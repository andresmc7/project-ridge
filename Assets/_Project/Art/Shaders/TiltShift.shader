// Desenfoque de maqueta (tilt-shift) en los bordes de la pantalla, como Echoes of Wisdom: la franja central queda
// nítida y arriba/abajo (y un poco las esquinas) se desenfocan con un disco de muestras en ángulo áureo. Va antes
// del post-proceso (HDR): las luces fuertes pesan más y se abren en discos de bokeh; el grano queda nítido encima.
Shader "Hidden/ProjectRidge/TiltShift"
{
    Properties
    {
        _FocusY ("Focus center (screen Y)", Range(0,1)) = 0.5
        _Band ("Sharp half-height", Range(0,0.5)) = 0.2
        _Feather ("Feather", Range(0.01,0.5)) = 0.22
        _MaxRadius ("Max blur radius (fraction of screen height)", Range(0,0.03)) = 0.009
        _Corners ("Corner blur", Range(0,1)) = 0.5
        _Highlight ("Bokeh highlight boost", Range(0,8)) = 3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "TiltShift"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FocusY, _Band, _Feather, _MaxRadius, _Corners, _Highlight;

            #define TAPS 20

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float dy = abs(uv.y - _FocusY);
                float dx = abs(uv.x - 0.5);
                float m = saturate((dy - _Band) / _Feather);
                m = max(m, saturate((dx - 0.36) / 0.16) * saturate((dy - _Band * 0.4) / _Feather) * _Corners);
                m = m * m * (3.0 - 2.0 * m);
                if (m < 0.02) return center;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 radius = float2(_MaxRadius / aspect, _MaxRadius) * m;
                half3 sum = 0;
                half wsum = 0;
                [unroll]
                for (int i = 0; i < TAPS; i++)
                {
                    float r = sqrt((i + 0.5) / TAPS);
                    float a = i * 2.39996;
                    float2 o = float2(cos(a), sin(a)) * r * radius;
                    half3 c = min(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + o).rgb, 4.0h); // tope: un brillo extremo no se expande
                    half l = dot(c, half3(0.2126, 0.7152, 0.0722));
                    half w = 1.0 + _Highlight * saturate(l - 0.6);
                    sum += c * w;
                    wsum += w;
                }
                return half4(sum / wsum, center.a);
            }
            ENDHLSL
        }
    }
}
