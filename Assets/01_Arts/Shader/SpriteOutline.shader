Shader "Effect/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness("Outline Thickness (px)", Range(0,8)) = 1
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
                float2 texel = _MainTex_TexelSize.xy;
                int T = clamp((int)ceil(_OutlineThickness), 1, 8);

                float dilatedAlpha = 0.0;
                [loop]
                for (int dy = -T; dy <= T; dy++)
                {
                    [loop]
                    for (int dx = -T; dx <= T; dx++)
                    {
                        float dist = length(float2(dx, dy));
                        float weight = 1.0 - smoothstep(_OutlineThickness - 0.5, _OutlineThickness + 0.5, dist);
                        if (weight <= 0.0) continue;

                        float2 offset = float2(dx, dy) * texel;
                        float sampledAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + offset).a;
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
