// Haz volumétrico del reflector del OVNI: cono abierto (ápice en el origen, base de radio 1 en z = 1), aditivo.
// En vez de sombrear la superficie del cono (que deja bordes duros y se vuelve una pared cuando la cámara está
// cerca), cada píxel intersecta su rayo con el cono de forma analítica, lo recorta contra la profundidad de la
// escena y acumula densidad en pocos pasos: núcleo denso, borde que se desvanece, polvo en movimiento, y fundido
// natural donde el haz toca el suelo. Se dibujan solo las caras traseras, así funciona también desde adentro.
Shader "ProjectRidge/VolumetricBeam"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.92, 1, 1)
        _Intensity ("Intensity", Float) = 1.2
        _Density ("Density (per m)", Float) = 0.07
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 0.18
        _NoiseSpeed ("Noise Speed", Float) = 0.35
        _CameraFade ("Camera Fade (m)", Float) = 6
        _EdgePower ("Edge Softness", Float) = 2.2
        _LengthFalloff ("Length Falloff", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
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

            #define STEPS 10

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _Density;
                float4 _NoiseTex_ST;
                float _NoiseScale;
                float _NoiseSpeed;
                float _CameraFade;
                float _EdgePower;
                float _LengthFalloff;
            CBUFFER_END
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float Dust(float3 p, float t)
            {
                float2 uvA = p.xz * _NoiseScale + float2(t, t * 0.6);
                float2 uvB = float2(p.x + p.z, p.y) * _NoiseScale * 0.7 + float2(-t * 0.4, t);
                return SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uvA, 0).r * SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uvB, 0).r * 2.2;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Geometría del cono en mundo, sacada de la matriz del objeto (escala = radio, radio, largo).
                float3 apex = TransformObjectToWorld(float3(0, 0, 0));
                float3 axisEnd = TransformObjectToWorld(float3(0, 0, 1));
                float3 axis = axisEnd - apex;
                float len = max(length(axis), 1e-3);
                float3 D = axis / len;
                float radius = length(TransformObjectToWorldDir(float3(1, 0, 0), false));
                float k = radius / len;
                float k2 = 1.0 + k * k;

                float3 cam = GetCameraPositionWS();
                float3 toBack = i.positionWS - cam;
                float tBack = length(toBack);
                float3 V = toBack / tBack;

                // Profundidad de la escena a lo largo del rayo.
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float tScene = sceneEye / max(dot(V, camFwd), 1e-3);

                // Entrada al cono: raíz de |X-A|^2 = (1+k^2) ((X-A)·D)^2 con (X-A)·D >= 0.
                float3 co = cam - apex;
                float dv = dot(V, D);
                float dco = dot(co, D);
                float a = 1.0 - dv * dv * k2;
                float b = 2.0 * (dot(V, co) - dv * dco * k2);
                float c = dot(co, co) - dco * dco * k2;
                float tEnter = max(tBack - 2.0 * radius, 0.0);
                bool inside = c <= 0.0 && dco >= 0.0 && dco <= len;
                if (inside) tEnter = 0.0;
                else
                {
                    if (abs(a) < 1e-4) a = 1e-4;
                    float disc = b * b - 4.0 * a * c;
                    if (disc >= 0.0)
                    {
                        float sq = sqrt(disc);
                        float r1 = (-b - sq) / (2.0 * a);
                        float r2 = (-b + sq) / (2.0 * a);
                        float tMin = min(r1, r2), tMax = max(r1, r2);
                        if (tMin > 0.0 && dco + tMin * dv >= 0.0) tEnter = tMin;
                        else if (tMax > 0.0 && dco + tMax * dv >= 0.0) tEnter = tMax;
                    }
                }
                float tExit = min(tBack, tScene);
                if (tExit <= tEnter) return 0;

                float step = (tExit - tEnter) / STEPS;
                float time = _Time.y * _NoiseSpeed;
                float acc = 0.0;
                [unroll]
                for (int s = 0; s < STEPS; s++)
                {
                    float t = tEnter + (s + 0.5) * step;
                    float3 x = cam + V * t;
                    float3 rel = x - apex;
                    float along = dot(rel, D);
                    float rs = max(along * k, 1e-3);
                    float radial = saturate(1.0 - length(rel - along * D) / rs);
                    radial = pow(radial, _EdgePower);                                   // núcleo denso, borde suave
                    float u = saturate(along / len);
                    float lengthFade = lerp(1.0, 0.3, pow(max(u, 1e-4), _LengthFalloff)) * smoothstep(0.0, 0.06, u);
                    float camFade = saturate((t - _ProjectionParams.y) / _CameraFade);
                    acc += radial * lengthFade * camFade * (0.45 + 0.9 * Dust(x, time));
                }
                float density = acc * step * _Density;
                float a01 = 1.0 - exp(-density);
                return half4(_Color.rgb * a01 * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
