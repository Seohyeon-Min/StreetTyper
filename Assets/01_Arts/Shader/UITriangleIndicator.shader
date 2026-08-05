Shader "UI/TriangleIndicator"
{
    // 위(▲)를 가리키는 정삼각형 하나만 그리는 UI 셰이더. 왼쪽/오른쪽을 가리키게 하려면
    // 셰이더가 아니라 오브젝트 자체를 90도 돌려서 쓴다(회전은 코드/에디터의 몫).
    //
    // ⚠️ 가로세로 비율 보정을 하지 않는다 - 이 오브젝트의 RectTransform을 정사각형(예: 40x40)
    // 으로 유지할 것. 직사각형으로 늘리면 삼각형도 같이 찌그러진다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (1,1,1,1)
        _Alpha ("Alpha (전체 불투명도, Fill Color와 별개로 전체를 한 번 더 곱함)", Range(0, 1)) = 1

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (안쪽으로 파고드는 두께, 0~0.5)", Range(0, 0.5)) = 0.12

        [Header(Shape)]
        _Roundness ("Corner Roundness (모서리 둥글기, 0=뾰족한 삼각형)", Range(0, 0.4)) = 0

        [Header(Edge)]
        _EdgeSoftness ("Edge Softness (안티앨리어싱 폭)", Range(0.0005, 0.05)) = 0.01

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        // UIStyle.shader와 같은 스텐실/클립 배선 - Mask/RectMask2D 밑에서도 정상적으로 잘린다.
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color        : COLOR;
                float2 uv          : TEXCOORD0;
                float4 positionWS  : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Alpha;
                float4 _ClipRect;
                float4 _MainTex_ST;
                half4 _OutlineColor;
                float _OutlineWidth;
                float _Roundness;
                float _EdgeSoftness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = IN.positionOS;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 꼭짓점이 위를 향하는 정삼각형 SDF(Inigo Quilez). p는 중심 기준 좌표, r은
            // 밑변 절반 폭에 해당하는 스케일이다. 셰이더 안에서 방향을 돌리지 않는다 -
            // 왼쪽/오른쪽을 가리키게 하려면 이 컴포넌트가 붙은 오브젝트 자체를 회전시킨다.
            float sdEquilateralTriangle(float2 p, float r)
            {
                const float k = sqrt(3.0);
                p.x = abs(p.x) - r;
                p.y = p.y + r / k;
                if (p.x + k * p.y > 0.0)
                    p = float2(p.x - k * p.y, -k * p.x - p.y) / 2.0;
                p.x -= clamp(p.x, -2.0 * r, 0.0);
                return -length(p) * sign(p.y);
            }

            half UnityGet2DClipping(in float2 position, in float4 clipRect)
            {
                half2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UV(0~1) -> 중심 기준 -1~1 좌표.
                float2 uvCentered = (IN.uv - 0.5) * 2.0;

                // 둥글리기: 더 작은 삼각형의 SDF를 구한 뒤 그 값에서 Roundness만큼 빼면
                // 모서리가 둥글게 부풀어 오른 모양이 된다(Inigo Quilez의 "rounding" 트릭) -
                // 기준 반지름(0.85)에서 Roundness를 미리 빼둬야 전체 크기가 유지된다.
                float sdf = sdEquilateralTriangle(uvCentered, 0.85 - _Roundness) - _Roundness;

                float edge = max(_EdgeSoftness, 0.0001);
                float fillAlpha = smoothstep(edge, -edge, sdf);

                // 채움 영역(sdf=0 기준)에서 안쪽으로 OutlineWidth만큼 줄인 영역을 뺀 나머지가
                // 테두리 링이다 - SpriteOutline/UIStyle의 윤곽선과 같은 발상.
                float outlineOuter = smoothstep(edge, -edge, sdf);
                float outlineInner = smoothstep(edge, -edge, sdf + _OutlineWidth);
                float outlineMask = saturate(outlineOuter - outlineInner);

                // ⚠️ _MainTex는 일부러 안 읽는다 - 이 삼각형은 SDF로 스스로 그리는 도형이라
                // Image에 배정된 스프라이트의 내용과 무관해야 한다. texColor.a를 곱해 쓰면
                // 배정된 텍스처(특히 세로줄 패턴이 있는 기본/placeholder 스프라이트)의 알파가
                // 그대로 도형 위에 섞여 들어가 줄무늬로 비친다 - 순수하게 계산한 색으로만 채운다.
                half4 col;
                col.rgb = lerp(IN.color.rgb, _OutlineColor.rgb, outlineMask * _OutlineColor.a);
                col.a = fillAlpha * IN.color.a * _Alpha;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.positionWS.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}
