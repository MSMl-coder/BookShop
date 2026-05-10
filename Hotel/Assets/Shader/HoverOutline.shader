// Assets/Shader/HoverOutline.shader
// Stencil-based outline — малює ТІЛЬКИ зовнішній контур.
// Працює з будь-якою геометрією включно з порожнистими мешами.
//
// Pass 1: рендерить об'єкт в stencil buffer (не видно на екрані)
// Pass 2: рендерить розширений об'єкт тільки де stencil = 0 → чистий зовнішній контур
//
// Матеріал: HoverOutlineMat
// Outline Width: 0.004 — 0.008 (підбирати під розмір об'єкта)

Shader "Custom/HoverOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Float) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        // ── Pass 1: Записуємо силует об'єкта в stencil ──────────
        // Не пишемо нічого в color або depth — тільки stencil
        Pass
        {
            Name "StencilWrite"
            Cull  Back
            ZTest LEqual
            ZWrite Off
            ColorMask 0   // не пишемо колір

            Stencil
            {
                Ref  1
                Comp Always
                Pass Replace  // де є геометрія — пишемо 1
            }

            HLSLPROGRAM
            #pragma vertex   vert_simple
            #pragma fragment frag_empty
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 pos : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert_simple(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                return OUT;
            }

            half4 frag_empty(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Pass 2: Малюємо розширений об'єкт де stencil = 0 ───
        // Результат: видно тільки пікселі що виходять за межі оригіналу = контур
        Pass
        {
            Name "OutlineDraw"
            Cull  Back
            ZTest Always   // поверх всього
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref  1
                Comp NotEqual  // малюємо тільки де stencil ≠ 1 (за межами об'єкта)
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex   vert_outline
            #pragma fragment frag_outline
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert_outline(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Розширення в clip space — рівномірна товщина
                float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);
                float3 clipNormal = mul((float3x3)UNITY_MATRIX_VP,
                                       mul((float3x3)UNITY_MATRIX_M, IN.normalOS));

                float2 offset = normalize(clipNormal.xy);
                offset.x /= _ScreenParams.x / _ScreenParams.y;
                clipPos.xy += offset * _OutlineWidth * clipPos.w;

                OUT.positionHCS = clipPos;
                return OUT;
            }

            half4 frag_outline(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
