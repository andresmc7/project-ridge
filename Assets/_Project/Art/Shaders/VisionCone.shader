// Cono de visión del Lurker sobre el suelo: abanico transparente, más denso hacia afuera, con un filo marcado al
// final del alcance y unas bandas que avanzan (se lee como "mirada", no como una mancha).
Shader "ProjectRidge/VisionCone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.2, 0.12, 0.16)
        _BandSpeed ("Band Speed", Float) = 0.6
        _Bands ("Bands", Float) = 5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _BandSpeed;
                half _Bands;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = i.uv.x;                                         // distancia / alcance
                float body = lerp(0.35, 1.0, d);
                float edge = smoothstep(0.93, 0.985, d) * (1.0 - smoothstep(0.985, 1.0, d));
                float band = 0.85 + 0.15 * sin((d * _Bands - _Time.y * _BandSpeed) * 6.2832);
                float a = _Color.a * (body * band + edge * 2.2);
                return half4(_Color.rgb * (1.0 + edge), saturate(a));
            }
            ENDHLSL
        }
    }
}
