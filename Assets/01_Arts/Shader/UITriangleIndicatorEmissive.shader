Shader "UI/TriangleIndicatorEmissive"
{
    // UITriangleIndicator.shader에 발광을 더한 버전. 도형/윤곽선/둥글기 계산은 그쪽과 완전히
    // 같고, 추가된 건 두 가지뿐이다 - 은은하게 밖으로 퍼지는 부드러운 후광(Glow)과, 시간에
    // 따라 밝기가 오르내리는 펄스.
    //
    // ⚠️ 이 프로젝트의 캔버스는 전부 Screen Space - Overlay라 URP Bloom(HDR 색상을 실제로
    // 번지게 하는 포스트 프로세싱)이 적용되지 않는다(오버레이는 카메라의 포스트 프로세싱을
    // 거치지 않고 화면에 바로 합성된다). 그래서 "빛난다"는 느낌을 실제 블룸이 아니라 셰이더
    // 안에서 흉내 낸다 - 도형 바깥으로 알파가 부드럽게 옅어지는 후광을 겹쳐 그리는 방식이다.
    // _EmissionColor에 [HDR]을 붙여둔 건 나중에 Screen Space - Camera + Bloom 조합으로
    // 바뀌어도 그때는 정말로 블룸을 받을 수 있게 하기 위해서다(지금은 그냥 밝은 색으로만 보인다).
    //
    // ⚠️ 가로세로 비율 보정을 하지 않는다 - RectTransform을 정사각형으로 유지할 것.
    // 왼쪽/오른쪽을 가리키게 하려면 오브젝트 자체를 90도 돌려서 쓴다(UITriangleIndicator와 동일).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (1,1,1,1)
        _Alpha ("Alpha (전체 불투명도)", Range(0, 1)) = 1

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (안쪽으로 파고드는 두께, 0~0.5)", Range(0, 0.5)) = 0.12

        [Header(Shape)]
        _Roundness ("Corner Roundness (모서리 둥글기, 0=뾰족한 삼각형)", Range(0, 0.4)) = 0

        [Header(Edge)]
        _EdgeSoftness ("Edge Softness (안티앨리어싱 폭)", Range(0.0005, 0.05)) = 0.01

        [Header(Glow)]
        [HDR] _EmissionColor ("Emission Color (HDR - Bloom을 받는 카메라 캔버스라면 진짜로 번짐)", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity (도형 안쪽 밝기 가산 + 후광 최대 밝기)", Range(0, 5)) = 1.5
        _GlowRadius ("Glow Radius (도형 바깥으로 후광이 퍼지는 거리)", Range(0, 0.6)) = 0.25
        _GlowFalloff ("Glow Falloff (1에 가까울수록 후광이 완만하게, 클수록 빠르게 사그라듦)", Range(0.5, 4)) = 1.5

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed (초당 몇 번 오르내릴지)", Range(0, 5)) = 1.2
        _PulseAmount ("Pulse Amount (0=고정 밝기, 1=꺼졌다 켜졌다)", Range(0, 1)) = 0.5

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

        // UITriangleIndicator.shader와 같은 스텐실/클립 배선 - Mask/RectMask2D 밑에서도 정상적으로 잘린다.
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

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
                half4 _EmissionColor;
                float _GlowIntensity;
                float _GlowRadius;
                float _GlowFalloff;
                float _PulseSpeed;
                float _PulseAmount;
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

            // UITriangleIndicator.shader와 동일 - 꼭짓점이 위를 향하는 정삼각형 SDF.
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
                float2 uvCentered = (IN.uv - 0.5) * 2.0;

                // ⚠️ 기준 반지름이 UITriangleIndicator(0.85)보다 작다(0.6) - 후광이 UV(-1~1)
                // 범위 밖으로는 못 그려지므로(오브젝트 자기 사각형 밖은 애초에 렌더링 대상이
                // 아니다), 삼각형 자체를 작게 그려 후광이 퍼질 여백을 안쪽에 미리 만들어둔다.
                float sdf = sdEquilateralTriangle(uvCentered, 0.6 - _Roundness) - _Roundness;

                float edge = max(_EdgeSoftness, 0.0001);
                float fillAlpha = smoothstep(edge, -edge, sdf);

                float outlineOuter = smoothstep(edge, -edge, sdf);
                float outlineInner = smoothstep(edge, -edge, sdf + _OutlineWidth);
                float outlineMask = saturate(outlineOuter - outlineInner);

                // 펄스: 0~1 사이를 오르내리며, PulseAmount로 진폭을 줄인다(0이면 항상 1로 고정).
                float pulseWave = sin(_Time.y * _PulseSpeed * 6.28318530718) * 0.5 + 0.5;
                float pulse = lerp(1.0, pulseWave, saturate(_PulseAmount));

                // 후광: 도형 바깥(sdf > 0)에서 GlowRadius만큼 멀어질 때까지 부드럽게 옅어진다.
                // 도형 안쪽(sdf <= 0)은 이미 채워져 있으니 후광 알파를 최대(1)로 둬서 안쪽부터
                // 바깥까지 밝기가 끊기지 않고 이어지게 한다.
                float outsideDist = max(sdf, 0.0);
                float glowFade = 1.0 - saturate(outsideDist / max(_GlowRadius, 0.0001));
                glowFade = pow(glowFade, max(_GlowFalloff, 0.0001));
                float glowAlpha = glowFade * _GlowIntensity * pulse * _EmissionColor.a;

                // ⚠️ _MainTex는 일부러 안 읽는다 - 이 삼각형은 SDF로 스스로 그리는 도형이라
                // Image에 배정된 스프라이트의 내용과 무관해야 한다. texColor.a를 곱해 쓰면
                // 배정된 텍스처(특히 세로줄 패턴이 있는 기본/placeholder 스프라이트)의 알파가
                // 그대로 도형 위에 섞여 들어가 줄무늬로 비친다 - 순수하게 계산한 색으로만 채운다.

                // 채움 + 윤곽선(UITriangleIndicator와 동일) 위에, 도형 안쪽에서는 발광 색을
                // 더해 밝게(가산) 만들고, 도형 바깥에서는 그 발광 색 자체를 후광으로 그린다.
                half3 baseRGB = lerp(IN.color.rgb, _OutlineColor.rgb, outlineMask * _OutlineColor.a);
                half3 emissiveAdd = _EmissionColor.rgb * _GlowIntensity * pulse * fillAlpha;

                half4 col;
                col.rgb = lerp(_EmissionColor.rgb, baseRGB + emissiveAdd, fillAlpha);

                // ⚠️ saturate 필수 - glowAlpha는 GlowIntensity가 1보다 크면 1을 넘을 수 있는데,
                // Blend SrcAlpha OneMinusSrcAlpha에서 알파가 1을 넘으면 (1-alpha)가 음수가 되어
                // 뒤 배경이 반전되어 비치는 것처럼 보이는 블렌딩 오류가 난다.
                col.a = saturate(max(fillAlpha * IN.color.a, glowAlpha) * _Alpha);

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
