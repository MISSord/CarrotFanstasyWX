// 2D 立绘单位 shader（URP）
// 模仿《明日方舟》3D 场景 + 2D 角色的混合方案：
// 1. Unlit，不受动态光照/阴影影响，保证塔防战斗中角色辨识度；
// 2. 可选 Billboard，使立绘始终面向相机；
// 3. 深度变换（_DepthStretch）：相机以 60° 俯角观察地面时，把角色顶点在
//    view space 相对 pivot 的深度偏移拉伸，让深度测试/写入表现得像角色
//    垂直于地面站立，从而解决 2D 立绘与 3D 地形的穿模问题。
Shader "G3/UnitSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Tint Color", Color) = (1, 1, 1, 1)
        _DepthStretch ("Depth Stretch", Range(0, 4)) = 0
        _FaceCamera ("Face Camera", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Tint;
            float _DepthStretch;
            float _FaceCamera;

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                if (_FaceCamera > 0.5)
                {
                    // Billboard：立绘面片绕自身 Y 轴转向相机（保持垂直站立感）。
                    float3 forward = normalize(GetCameraPositionWS().xyz - positionWS);
                    forward.y = 0;
                    forward = normalize(forward);
                    float3 right = normalize(cross(float3(0, 1, 0), forward));
                    float3 up = cross(forward, right);
                    positionWS = GetObjectToWorldMatrix()._m03_m13_m23
                        + right * input.positionOS.x
                        + up * input.positionOS.y;
                }

                // 深度变换：顶点在 view space 中相对 pivot（物体原点）的深度偏移拉伸。
                // 相机俯角 60° 时，2D 立绘"躺"在地面上，深度测试需要表现得垂直于地面，
                // 理论拉伸系数 1 / sin(60°)。数值 0 表示关闭。
                float3 viewPos = TransformWorldToView(positionWS);
                float3 pivotWS = GetObjectToWorldMatrix()._m03_m13_m23;
                float3 pivotView = TransformWorldToView(pivotWS);
                float depthOffset = viewPos.z - pivotView.z;
                viewPos.z = pivotView.z + depthOffset * max(_DepthStretch, 1.0);
                // view space → clip space：直接用投影矩阵手动变换，兼容所有 URP 版本。
                output.positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return texColor * input.color * _Tint;
            }
            ENDHLSL
        }
    }
}
