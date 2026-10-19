// Haz volumétrico falso para el reflector del OVNI: cono abierto (ápice en el origen, base en z = 1) aditivo.
// La densidad sube hacia el eje visto de frente (Fresnel invertido), cae con la distancia al OVNI, se mueve con
// ruido (polvo y humedad en el aire) y se funde suavemente donde toca el suelo y cerca de la cámara.
Shader "ProjectRidge/VolumetricBeam"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.92, 1, 1)
        _Intensity ("Intensity", Float) = 1.2
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 0.18
        _NoiseSpeed ("Noise Speed", Float) = 0.35
        _SoftDistance ("Soft Intersection (m)", Float) = 2.5
        _CameraFade ("Camera Fade (m)", Float) = 3
        _EdgePower ("Edge Softness", Float) = 2.2
        _LengthFalloff ("Length Falloff", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

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
                float4 _NoiseTex_ST;
                float _NoiseScale;
                float _NoiseSpeed;
                float _SoftDistance;
                float _CameraFade;
                float _EdgePower;
                float _LengthFalloff;
            CBUFFER_END
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float along : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.along = saturate(v.positionOS.z);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = abs(dot(normalize(i.normalWS), view));
                float core = pow(saturate(facing), _EdgePower);                  // denso en el centro, sin borde duro

                float t = _Time.y * _NoiseSpeed;
                float2 uvA = i.positionWS.xz * _NoiseScale + float2(t, t * 0.6);
                float2 uvB = float2(i.positionWS.x + i.positionWS.z, i.positionWS.y) * _NoiseScale * 0.7 + float2(-t * 0.4, t);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvA).r * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvB).r * 2.2;

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float frag = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float soft = saturate((scene - frag) / _SoftDistance);
                float camFade = saturate((frag - _ProjectionParams.y) / _CameraFade);
                float lengthFade = lerp(1.0, 0.35, pow(max(i.along, 1e-4), _LengthFalloff)) * smoothstep(0.0, 0.08, i.along);

                float a = core * (0.55 + 0.9 * noise) * soft * camFade * lengthFade * _Intensity;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
