// Assets/Shader/HoverHighlight.shader
// Hover ефект — вибілювання об'єкта при наведенні мишки.
// Рендерується поверх оригіналу як напівпрозорий білий шар.
//
// Параметри в матеріалі HoverOutlineMat:
//   Highlight Color — колір підсвітки (default білий)
//   Highlight Alpha — сила ефекту (0 = невидимо, 1 = повністю білий)
//                     рекомендовано 0.15 - 0.35
//   Depth Offset    — якщо мерехтить, збільш до 0.01

Shader "Custom/HoverHighlight"
{
    Properties
    {
        _HighlightColor ("Highlight Color", Color)    = (1, 1, 1, 1)
        _HighlightAlpha ("Highlight Alpha", Range(0, 1)) = 0.25
        _DepthOffset    ("Depth Offset",    Float)    = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "HoverHighlight"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull  Back
            ZTest LEqual
            ZWrite Off

            // Адитивне змішування — просто додає білий колір поверх
            Blend One OneMinusSrcAlpha

            // Невеликий offset щоб уникнути z-fighting з оригінальним мешем
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HighlightColor;
                float  _HighlightAlpha;
                float  _DepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Білий колір з налаштованою прозорістю
                return half4(_HighlightColor.rgb * _HighlightAlpha, _HighlightAlpha);
            }
            ENDHLSL
        }
    }
}
