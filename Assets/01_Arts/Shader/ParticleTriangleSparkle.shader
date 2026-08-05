Shader "Custom/ParticleTriangleSparkle"
{
    // UITriangleIndicatorEmissive.shader의 삼각형+발광+펄스를 파티클(Shuriken)에 맞게 옮긴
    // 버전. UI용 스텐실/클립 배선은 없고(파티클은 캔버스 밖 월드에서 돈다) HitImpact.shader와
    // 같은 SRPDefaultUnlit + 가산 블렌드를 쓴다.
    //
    // ⚠️ Start Color/Color over Lifetime는 안 쓴다 - 파티클 시스템이 굽는 정점 컬러를 셰이더가
    // 얼마나 읽어서 쓰는지에 달렸는데(HitImpact.shader가 RGB만 읽고 알파는 무시하는 것과 같은
    // 함정), 그 대신 파티클마다 고정된 무작위 시드 하나(Custom Data)를 받아 그걸 해시해서
    // 반짝임 위상·색 변위를 만든다. 위치를 해시하는 방식과 달리 파티클이 움직여도(수명 내내)
    // 값이 안 변한다 - Custom Data는 스폰 시점에 한 번 구워지는 진짜 파티클별 고정값이다.
    //
    // ⚠️ 설정이 필요하다 - Particle System에서:
    //   1) Modules > Custom Data 를 켜고 Particle 쪽 Custom1을 Vector(1)로, Random Between
    //      Two Constants로 0~1 범위를 준다(파티클마다 스폰 시 무작위로 하나씩 뽑힌다).
    //   2) Renderer 모듈 > Custom Vertex Streams 에서 "Custom1.x"를 추가한다.
    // 이 두 개를 안 하면 customSeed가 항상 0으로 들어와 모든 파티클이 같은 위상/색으로 보인다.
    Properties
    {
        [Header(Shape)]
        _Roundness ("Corner Roundness (모서리 둥글기, 0=뾰족한 삼각형)", Range(0, 0.4)) = 0.1
        _EdgeSoftness ("Edge Softness (안티앨리어싱 폭)", Range(0.0005, 0.05)) = 0.02

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width (안쪽으로 파고드는 두께, 0~0.5)", Range(0, 0.5)) = 0.12

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color (기준 색 - 파티클마다 여기서 살짝 벗어난다)", Color) = (1,1,1,1)
        _ColorVariation ("Color Variation (파티클마다 RGB 채널별로 벗어나는 정도, 0=전부 같은 색)", Range(0, 1)) = 0.3

        // 반짝임 최대/최소
        [Header(Sparkle)]
        _GlowMin ("Glow Min (가장 어두울 때 밝기)", Range(0, 5)) = 0.3
        _GlowMax ("Glow Max (가장 밝을 때 밝기)", Range(0, 5)) = 3.0
        _SparkleSpeed ("Sparkle Speed (초당 몇 번 오르내릴지)", Range(0, 10)) = 2.0

        [Header(Random)]
        _RandomSeed ("Random Seed (바꾸면 파티클마다의 반짝임/색 패턴이 통째로 다시 섞인다)", Float) = 0

        _Alpha ("Alpha (전체 불투명도)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        // HitImpact.shader와 같은 순수 가산 블렌드 - 겹칠수록 더 밝아지고, 배경은 그대로 비친다.
        Blend One One

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                // Renderer 모듈 > Custom Vertex Streams에서 "Custom1.x"를 여기(TEXCOORD1)에
                // 매핑해야 한다 - 파티클마다 스폰 시 한 번 정해지는 고정 무작위 시드(0~1).
                float customSeed  : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float customSeed  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Roundness;
                float _EdgeSoftness;
                half4 _OutlineColor;
                float _OutlineWidth;
                half4 _EmissionColor;
                float _ColorVariation;
                float _GlowMin;
                float _GlowMax;
                float _SparkleSpeed;
                float _RandomSeed;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.customSeed = IN.customSeed;
                return OUT;
            }

            // 꼭짓점이 위를 향하는 정삼각형 SDF(Inigo Quilez) - UITriangleIndicator와 동일.
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

            // 파티클의 고정 시드(customSeed) + offset으로 0~1 사이 무작위 값을 만든다.
            // offset을 바꿔 부르면(예: hash11(seed, 0) vs hash11(seed, 11.0)) 같은 파티클
            // 안에서도 서로 다른(상관없는) 무작위 값을 여러 개 뽑아 쓸 수 있다.
            float hash11(float x, float offset)
            {
                float3 p = frac((float3(x, x, x) + offset) * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uvCentered = (IN.uv - 0.5) * 2.0;

                float sdf = sdEquilateralTriangle(uvCentered, 0.85 - _Roundness) - _Roundness;

                float edge = max(_EdgeSoftness, 0.0001);
                float fillAlpha = smoothstep(edge, -edge, sdf);

                float outlineOuter = smoothstep(edge, -edge, sdf);
                float outlineInner = smoothstep(edge, -edge, sdf + _OutlineWidth);
                float outlineMask = saturate(outlineOuter - outlineInner);

                // 파티클별 무작위 값 셋 - IN.customSeed(Custom Data로 스폰 시 정해진 고정값)에
                // _RandomSeed(전체를 다시 섞는 재질 쪽 오프셋)를 더한 뒤, 서로 다른 offset으로
                // 상관없는 값 4개를 뽑는다.
                float baseSeed = IN.customSeed + _RandomSeed;
                float phaseSeed = hash11(baseSeed, 0.0);
                float colorSeedR = hash11(baseSeed, 11.0) * 2.0 - 1.0;
                float colorSeedG = hash11(baseSeed, 23.0) * 2.0 - 1.0;
                float colorSeedB = hash11(baseSeed, 37.0) * 2.0 - 1.0;

                // 반짝임: 파티클마다 위상(phaseSeed)이 달라 서로 어긋난 채로 GlowMin~GlowMax
                // 사이를 오르내린다 - 전부 같은 박자로 깜빡이지 않고 각자 따로 반짝인다.
                float sparkleWave = sin((_Time.y * _SparkleSpeed + phaseSeed * 6.28318530718)) * 0.5 + 0.5;
                float glow = lerp(_GlowMin, _GlowMax, sparkleWave);

                // 색 변위: 파티클마다 RGB 채널을 각각 독립적으로 +-ColorVariation만큼 밝기를
                // 어긋내서, 전부 똑같은 색이 아니라 은은하게 다른 색조로 반짝이게 한다.
                float3 colorNoise = float3(colorSeedR, colorSeedG, colorSeedB) * _ColorVariation;
                half3 emissiveColor = _EmissionColor.rgb * (1.0 + colorNoise);

                half3 rgb = lerp(emissiveColor, _OutlineColor.rgb, outlineMask * _OutlineColor.a);
                rgb *= glow * fillAlpha * IN.color.rgb;

                float alpha = fillAlpha * IN.color.a * _EmissionColor.a * _Alpha;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
