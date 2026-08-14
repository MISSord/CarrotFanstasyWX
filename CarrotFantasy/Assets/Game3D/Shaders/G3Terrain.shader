// 3D 网格地形 shader（URP，Unlit + 顶点色）
// 用于渲染方形/六边形 3D 地形块：
// 1. 顶点色驱动，每块地形可单独配色（可部署/不可部署/高亮等）；
// 2. 可选网格线高亮（_GridLineWidth），便于开发期观察格点布局；
// 3. 不参与光照计算（Unlit），场景氛围由烘焙 Lightmap + tint 统一控制，
//    与《明日方舟》"场景写实、格点抽象"的视觉策略一致。
Shader "G3/Terrain"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _GridColor ("Grid Line Color", Color) = (0.2, 0.2, 0.2, 1)
        _GridLineWidth ("Grid Line Width", Range(0, 0.5)) = 0.05
        _GridUvScale ("Grid UV Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            half4 _GridColor;
            float _GridLineWidth;
            float _GridUvScale;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float fogCoord : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half3 color = albedo.rgb * input.color.rgb;

                // 网格线：基于 UV 小数部分到整数边界的距离绘制。
                float2 gridUv = input.uv * _GridUvScale;
                float2 edgeDist = min(frac(gridUv), 1.0 - frac(gridUv));
                float lineMask = 1.0 - smoothstep(0.0, _GridLineWidth, min(edgeDist.x, edgeDist.y));
                color = lerp(color, _GridColor.rgb, lineMask * _GridColor.a);

                color = MixFog(color, input.fogCoord);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
