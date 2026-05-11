// Assets/Shader/HoverOutline.shader
// Простий Unlit матеріал що додається як другий матеріал на renderer.
// Малює об'єкт повністю білим — разом з основним матеріалом виглядає як outline
// якщо використати невеликий scale offset в вертексному шейдері.

Shader "Custom/HoverOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 0.8)
        _OutlineWidth ("Outline Width", Float) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+1"
        }

        Pass
        {
            Name "HoverOutline"
            Cull  Front          // тільки задні грані (inverted hull)
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
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

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Розширення в clip space — рівномірна товщина на будь-якій відстані
                float4 clip   = TransformObjectToHClip(IN.positionOS.xyz);
                float3 clipN  = mul((float3x3)UNITY_MATRIX_VP,
                                mul((float3x3)UNITY_MATRIX_M, IN.normalOS));
                float2 offset = normalize(clipN.xy);
                offset.x     /= _ScreenParams.x / _ScreenParams.y;
                clip.xy      += offset * _OutlineWidth * clip.w;

                OUT.positionHCS = clip;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
