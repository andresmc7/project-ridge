// Banco de niebla baja: tarjeta horizontal o vertical, mezcla alfa, ruido que se desplaza, fundido suave contra la
// geometría (no se ven cortes), contra la cámara y cuando se mira de canto. Toma color de la luz ambiente.
Shader "ProjectRidge/FogCard"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.62, 0.7, 0.35)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 0.08
        _Speed ("Drift Speed", Vector) = (0.05, 0.02, 0, 0)
        _SoftDistance ("Soft Intersection (m)", Float) = 1.5
        _CameraFade ("Camera Fade (m)", Float) = 4
        _EdgeFade ("Card Edge Fade", Range(0.01, 0.5)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _NoiseTex_ST;
                float _NoiseScale;
                float4 _Speed;
                float _SoftDistance;
                float _CameraFade;
                float _EdgeFade;
            CBUFFER_END
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz * _NoiseScale;
                float t = _Time.y;
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p + _Speed.xy * t).r;
                n *= SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p * 1.9 - _Speed.yx * t * 1.3).r * 1.8;

                float2 edge = min(i.uv, 1.0 - i.uv);
                float card = smoothstep(0.0, _EdgeFade, min(edge.x, edge.y));

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float frag = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float soft = saturate((scene - frag) / _SoftDistance);
                float camFade = saturate((frag - _ProjectionParams.y) / _CameraFade);
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float grazing = saturate(abs(dot(normalize(i.normalWS), view)) * 3.0);

                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 col = _Color.rgb * (0.6 + ambient * 1.5);
                half a = _Color.a * n * card * soft * camFade * grazing;
                col = MixFog(col, i.fog);
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
}
