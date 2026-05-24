// Assets/Shader/NPC_UI_NoDepth.shader  ← НАЙПРОСТІШЕ РІШЕННЯ
// Призначити як матеріал на BubbleBg Image компонент.
//
// Відмінність від стандартного UI/Default:
//   ZWrite Off  ← Canvas НЕ пише в depth buffer
//   ZTest Always ← малюється поверх 3D геометрії (як нормальний UI)
//
// SG_LigneClaireOutline читає depth — якщо Canvas не пише depth,
// шейдер не бачить геометрію Canvas і не малює лінії.
//
// SETUP:
//   1. Assets → Create → Material → "NPC_Bubble_Mat"
//   2. Shader: Custom/NPC_UI_NoDepth
//   3. На Image компонент BubbleBg → Material → NPC_Bubble_Mat
//   4. Якщо хмаринка стала невидима за 3D об'єктами — це нормально,
//      бо ZTest Always малює її поверх всього. Якщо потрібна глибина —
//      замінити ZTest на LEqual (але тоді depth пишеться знову).

Shader "Custom/NPC_UI_NoDepth"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D)    = "white" {}
        _Color   ("Tint",           Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Overlay"       // малюється після всього 3D
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Cull   Off
        ZWrite Off          // ← КЛЮЧОВА РІЗНИЦЯ: не пишемо в depth buffer
        ZTest  Always       // ← малюється поверх усього (як Screen Space UI)
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "NPC_UI_NoDepth"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return col * IN.color;
            }
            ENDHLSL
        }
    }

    // Fallback для старих Unity версій
    FallBack "UI/Default"
}
