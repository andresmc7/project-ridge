// Círculo de luz del reflector proyectado sobre la escena: se dibuja un cubo alrededor del círculo y cada píxel
// reconstruye su posición en mundo desde la profundidad. Así la luz cae sobre el terreno, las rocas, el pasto y
// el propio papá, sin usar una luz por reflector. Núcleo suave + anillo brillante en el borde (aviso) + polvo.
Shader "ProjectRidge/LightPool"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.12, 0.07, 1)
        _Intensity ("Intensity", Float) = 1.6
        _RimIntensity ("Rim Intensity", Float) = 2.4
        _RimWidth ("Rim Width", Range(0.01, 0.3)) = 0.07
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 0.35
        _Alarm ("Alarm", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _RimIntensity;
                half _RimWidth;
                float4 _NoiseTex_ST;
                float _NoiseScale;
                half _Alarm;
            CBUFFER_END
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 local = TransformWorldToObject(world);
                if (abs(local.x) > 0.5 || abs(local.z) > 0.5 || abs(local.y) > 0.5) return 0;

                // Solo cae sobre superficies que miran hacia arriba (suelo, rocas, pasto). Las verticales no reciben nada:
                // con un 25% el círculo trepaba por la pared del escalón de terraza junto al haz y, con el anillo encima,
                // dibujaba el costado de un balde. Al papá lo ilumina la luz spot real del reflector (SearchlightLight).
                float3 n = normalize(cross(ddy(world), ddx(world)));
                float facing = smoothstep(0.45, 0.8, abs(n.y));
                float r = length(local.xz) * 2.0;                       // 0 centro, 1 borde
                // Borde ondulado (±6%, ondas de varios metros) que se mueve despacio: un círculo de compás perfecto se leía como un disco
                // sólido apoyado en el suelo; la luz real se rompe contra el pasto y las piedras.
                float wobble = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, world.xz * 0.12 + _Time.y * 0.02).r;
                r *= 1.0 + (wobble - 0.5) * 0.12;
                if (r > 1.0) return 0;
                float core = 1.0 - r * r;
                core *= core;
                float rim = smoothstep(1.0 - _RimWidth * 2.0, 1.0 - _RimWidth, r) * (1.0 - smoothstep(1.0 - _RimWidth * 0.5, 1.0, r));
                // Borde fino que parpadea (POLISH_PASS 3.10). Sin banda brillante junto al borde: con ella y el anillo el
                // círculo tenía contorno de objeto; ahora el relleno cae a cero hacia el borde y solo queda el aviso fino.
                float ang = atan2(local.z, local.x);
                float flick = 0.6 + 0.4 * sin(_Time.y * 23.0 + ang * 3.0) * sin(_Time.y * 7.3 + ang);
                rim *= flick;
                // Se apaga hacia arriba y abajo del volumen y llega a cero ANTES del límite de la caja: antes quedaba en
                // 60% en el límite y cortaba en línea recta las rocas que asomaban por encima (sala 5, S4).
                float height = 1.0 - smoothstep(0.2, 0.5, abs(local.y));
                height *= facing;

                float t = _Time.y;
                float grain = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, world.xz * _NoiseScale + float2(t * 0.05, -t * 0.04)).r;
                float dust = 0.75 + 0.5 * grain;

                float pulse = 1.0 + _Alarm * 1.5;
                float3 col = _Color.rgb * (core * _Intensity * dust + rim * _RimIntensity) * height * pulse;
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
}
