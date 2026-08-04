Shader "Effect/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness (px)", Range(0,8)) = 1
        _OutlineMinSpriteThickness("Outline Min Sprite Thickness (px, 원본이 이보다 얇으면 그 부분엔 윤곽 안 그림)", Range(0,3)) = 1
        // 애니메이션 프레임처럼 한 텍스처에 스프라이트가 여러 장 붙어 있을 때(Sprite Multiple,
        // 패딩 0) 팽창 샘플링이 옆 프레임까지 읽어 그 윤곽까지 같이 그려지는 문제가 있다.
        // SpriteOutlineUVSync.cs가 SpriteRenderer.sprite의 UV 사각형을 여기 흘려주면 그 범위
        // 밖은 아예 샘플링하지 않는다 - 스크립트가 안 붙어 있으면 기본값(0,0,1,1)이라 기존과 동일.
        [PerRendererData] _SpriteUVRect ("Sprite UV Rect (xMin,yMin,xMax,yMax)", Vector) = (0,0,1,1)
        _SpriteRectInset ("Sprite Rect Inset (px, 옆 프레임 침범 방지 여유)", Range(0,4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        // URP 2D 렌더러는 같은 LightMode 태그의 Pass가 여러 개 있어도 첫 번째만
        // 그리는 경우가 많아, 원본 스프라이트 + 윤곽선을 하나의 Pass 안에서 함께 처리한다.
        // 알파 임계값으로 "원본이냐 윤곽선이냐"를 분기하면 안티앨리어싱된 반투명 가장자리에서
        // 윤곽선이 비어 보이는 틈이 생기므로, 윤곽선을 뒤에 깔고 원본을 프리멀티플라이드 알파로
        // 그 위에 합성한다.
        Pass
        {
            Name "SpriteWithOutline"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineThickness;
                float _OutlineMinSpriteThickness;
                float4 _SpriteUVRect;
                float _SpriteRectInset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                const VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float4 baseCol = texCol * _Color * IN.color;
                float baseAlpha = saturate(baseCol.a);

                // Morphological Dilation(팽창): (2T+1)x(2T+1) 정사각 커널 전체를 훑되,
                // 실제 유클리드 거리(dist <= thickness)인 픽셀만 채택해서 원형 커널로 만든다 —
                // 격자를 빠짐없이 훑으므로 반지름이 커져도 빈틈이 안 생긴다.
                // 커널 바깥쪽 가장자리는 smoothstep으로 살짝 페더링해서 계단현상을 줄인다(안티앨리어싱).
                // 페더 폭은 일부러 좁게(±0.15px) 잡았다 - 기존 ±0.5px는 윤곽선 바깥쪽이
                // 눈에 띄게 번져 보였다.
                float2 texel = _MainTex_TexelSize.xy;
                int T = clamp((int)ceil(_OutlineThickness), 1, 8);

                // 머리카락 한 가닥처럼 원본 스프라이트에서 가늘게 삐져나온 부분은 윤곽선의
                // 소스로 쓰지 않는다 - 침식(erosion) 테스트: 후보 픽셀 주변 반경
                // minR(= _OutlineMinSpriteThickness) 안이 전부 불투명해야("두껍다") 그 픽셀을
                // 윤곽선 소스로 인정한다. 가는 선은 반지름만큼의 폭이 없어 주변에 투명 픽셀이
                // 섞여 있으므로 이 테스트를 항상 못 넘고, 그 결과 그 부분엔 윤곽선이 안 생긴다.
                int minR = clamp((int)round(_OutlineMinSpriteThickness), 0, 3);

                // 이 스프라이트 자신의 UV 사각형. 밖으로는 절대 샘플링하지 않는다(옆 프레임 침범 방지).
                // ⚠️ 경계값 그대로 클램프하면 부족하다 - 바이리니어 필터링은 딱 그 경계 UV에서도
                // 옆 프레임의 첫 텍셀과 이 프레임의 마지막 텍셀을 섞어버린다(패딩 0이라 두 텍셀이
                // 바로 붙어 있다). 여유 폭(_SpriteRectInset, 기본 1.5px)만큼 더 안쪽으로 물러나
                // 텍셀 중심 안쪽에서만 샘플링하게 한다 - 사각형이 여유 폭보다 작은 극단적인
                // 경우를 대비해 중심점을 넘어서까지 좁혀지지 않도록 가운데로 클램프한다.
                float2 inset = texel * _SpriteRectInset;
                float2 uvCenter = (_SpriteUVRect.xy + _SpriteUVRect.zw) * 0.5;
                float2 uvMin = min(_SpriteUVRect.xy + inset, uvCenter);
                float2 uvMax = max(_SpriteUVRect.zw - inset, uvCenter);

                float dilatedAlpha = 0.0;
                [loop]
                for (int dy = -T; dy <= T; dy++)
                {
                    [loop]
                    for (int dx = -T; dx <= T; dx++)
                    {
                        float dist = length(float2(dx, dy));
                        float weight = 1.0 - smoothstep(_OutlineThickness - 0.15, _OutlineThickness + 0.15, dist);
                        if (weight <= 0.0) continue;

                        float2 uv2 = clamp(IN.uv + float2(dx, dy) * texel, uvMin, uvMax);
                        float sampledAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2).a;
                        if (sampledAlpha <= 0.0) continue;

                        if (minR > 0)
                        {
                            float minLocalAlpha = sampledAlpha;
                            [loop]
                            for (int ey = -minR; ey <= minR; ey++)
                            {
                                [loop]
                                for (int ex = -minR; ex <= minR; ex++)
                                {
                                    if (ex == 0 && ey == 0) continue;
                                    if (length(float2(ex, ey)) > minR + 0.5) continue;

                                    float2 euv = clamp(uv2 + float2(ex, ey) * texel, uvMin, uvMax);
                                    minLocalAlpha = min(minLocalAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, euv).a);
                                }
                            }
                            // 주변 반경이 전부 불투명하지 않다 = 가는 부분이다 - 소스에서 제외.
                            if (minLocalAlpha < 0.5) continue;
                        }

                        dilatedAlpha = max(dilatedAlpha, sampledAlpha * weight);
                    }
                }

                float outlineAlpha = saturate(dilatedAlpha * _OutlineColor.a * IN.color.a);

                // 윤곽선을 뒤에 놓고 원본을 위에 합성 — 반투명 가장자리의 남은 공간을
                // 윤곽선 색으로 채워서 원본과 윤곽선 사이에 틈이 생기지 않는다.
                float combinedAlpha = baseAlpha + outlineAlpha * (1.0 - baseAlpha);

                // Blend One OneMinusSrcAlpha용 프리멀티플라이드 색상.
                float3 combinedRGB =
                    baseCol.rgb * baseAlpha +
                    _OutlineColor.rgb * outlineAlpha * (1.0 - baseAlpha);

                return float4(combinedRGB, combinedAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
