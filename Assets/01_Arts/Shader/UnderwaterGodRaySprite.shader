Shader "Effect/UnderwaterGodrayURP_Sprite"
{
    Properties
    {
        [PerRendererData] [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color("Glow Color", Color) = (1,0.9,0.8,1)
        _Intensity("Glow Intensity", Range(0,2)) = 0.4
        _AlphaScale("Alpha Scale", Range(0,2)) = 1.0
        _Speed("Flow Speed", Range(0,2)) = 0.4
        _LineCount1("Line Count 1 (rays around center)", Range(1,40)) = 8
        _LineCount2("Line Count 2 (crossing layer)", Range(1,40)) = 16
        _LineWidth("Line Width (fraction of gap between lines)", Range(0.05,0.95)) = 0.35
        _LineSharpness("Line Edge Sharpness (higher = crisper cutoff)", Range(1,32)) = 8.0
        _LineJitter("Line Jitter (0 = straight cartoon lines, 1 = organic wobble)", Range(0,1)) = 0.3
        _TopFadeIn("Top Fade In", Range(0,1)) = 0.05
        _TopFadeOut("Top Fade Out", Range(0,1)) = 0.96
        _YOffset("Polar Y Offset", Range(0,4)) = 2.0
        _PolarScale("Polar Scale", Range(1,100)) = 50.0
        _CenterX("Center X (0..1)", Range(0,1)) = 0.7
        _Aspect("Aspect (Width / Height)", Range(0.1,10)) = 1.0
        _BackgroundTex("Background Texture (Glow Dodge base)", 2D) = "black" {}
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

        Pass
        {
            Name "SpriteUnlit"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BackgroundTex);
            SAMPLER(sampler_BackgroundTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity, _AlphaScale, _Speed;
                float _LineCount1, _LineCount2, _LineWidth;
                float _TopFadeIn, _TopFadeOut, _YOffset, _PolarScale, _CenterX;
                float _Aspect;
                float _LineSharpness;
                float _LineJitter;
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

            float hash1d(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            // 각도(angle, 라디안)를 lineCount개의 칸으로 등분하고 각 칸 가운데에만 선을 그린다.
            // 칸 너비(lineWidth)가 항상 1보다 작게 클램프되므로 선 사이 간격이 구조적으로 보장된다 —
            // 즉 두 선이 우연히 붙어서 하나의 면으로 보이는 일이 없다.
            float RayLines(float angle, float lineCount, float lineWidth, float sharpness, float jitter, float flowT)
            {
                float cell = angle / (2.0 * PI) * lineCount + flowT * lineCount;
                float idx = floor(cell);
                float f = frac(cell) - 0.5; // -0.5..0.5, 0 = 칸 중앙(선의 중심)

                // 지터: 선 위치와 두께를 칸별로 살짝 무작위화하되, 이웃 칸과 절대 겹치지 않도록 상한을 둔다.
                float centerJitter = (hash1d(idx) - 0.5) * jitter * 0.3;
                float widthJitter = 1.0 + (hash1d(idx + 91.7) - 0.5) * jitter * 0.6;

                float halfWidth = min(saturate(lineWidth * 0.5 * widthJitter), 0.45);
                float d = abs(f - centerJitter);

                float edge = 0.5 / max(sharpness, 1.0);
                return 1.0 - smoothstep(halfWidth - edge, halfWidth + edge, d);
            }

            float2 cart2polar(float2 xy)
            {
                float phi = atan2(xy.y, xy.x);
                float r   = length(xy);
                return float2(phi, r);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float2 v = (uv - float2(_CenterX, 0.5)) * 2.0;
                v.x *= _Aspect;
                v.y -= _YOffset;
                v /= _PolarScale;
                float2 polar = cart2polar(v);

                float t = _Time.y * _Speed;

                float n1 = RayLines(polar.x, _LineCount1, _LineWidth, _LineSharpness, _LineJitter, 0.04 * t);
                float n2 = RayLines(polar.x, _LineCount2, _LineWidth, _LineSharpness, _LineJitter, -0.06 * t);
                // min이 아니라 max: 두 레이어의 선을 각각 "그려서 겹치는" 형태라 겹치는 부분만
                // 보이는 게 아니라 둘 중 하나라도 선이면 보인다. 겹치는 곳이 더 밝아지는 걸 원하면
                // n1 + n2 - n1 * n2 (스크린 합성)로 바꿔도 된다.
                float n3 = max(n1, n2);

                float mask = smoothstep(_TopFadeIn, _TopFadeOut, uv.y);
                float brightness = n3 * mask * _Intensity;

                // 발광 닷지(Glow Dodge): result = blend / (1 - base).
                // base는 _BackgroundTex(배경 스프라이트)에서 같은 UV로 직접 읽는다 —
                // 프레임버퍼를 읽는 게 아니라 배경 텍스처를 그대로 참조하는 방식이라
                // Blend 스테이트는 일반 알파 블렌드로 되돌리고, 합성 자체는 여기서 끝낸다.
                float3 base = SAMPLE_TEXTURE2D(_BackgroundTex, sampler_BackgroundTex, uv).rgb;
                float3 blendColor = _Color.rgb * baseCol.rgb * brightness;
                float3 col = saturate(blendColor / max(1.0 - base, 0.0001));

                float alpha = brightness * baseCol.a * _AlphaScale;

                return float4(col, alpha) * IN.color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
