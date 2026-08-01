Shader "Custom/HitImpact"
{
    Properties
    {
        [Header(Progress)]
        _Progress ("Progress (0~1, 외부 스크립트에서 제어)", Range(0, 1)) = 0
        _Duration ("Duration (몇 초 안에 재생을 끝낼지 - EffectBase가 참고만 하는 값)", Range(0.05, 2)) = 0.2
        _FadeStart ("Fade Start (여기부터 서서히 사라짐)", Range(0.1, 0.95)) = 0.6
        _GrowthPower ("Growth Power (1=일정 속도, 1보다 작으면 처음에 빨리 커지다 느려짐, 1보다 크면 처음엔 느리다가 나중에 확 커짐)", Range(0.1, 5)) = 1.0

        [Header(Color)]
        [HDR] _Color ("Outline Color (윤곽선 색)", Color) = (1, 1, 1, 1)

        [Header(Cloud Outline Shape)]
        _MaxRadius ("Max Radius (최대로 퍼지는 반경)", Range(0.3, 2)) = 1.0
        _RingWidth ("Ring Width (윤곽선 두께)", Range(0.005, 0.6)) = 0.18
        _EdgeSoftness ("Edge Softness (클수록 구름처럼 부드럽게 퍼짐)", Range(0.01, 0.6)) = 0.15
        _BumpScale ("Bump Scale (구름 뭉게뭉게 개수, 작을수록 크고 둥글게)", Range(1, 8)) = 3
        _BumpStrength ("Bump Strength (경계가 울퉁불퉁 부풀어 오르는 정도)", Range(0, 1)) = 0.4
        _Seed ("Random Seed (값을 바꾸면 다른 모양)", Range(0, 100)) = 0

        [Header(Outline Breaks)]
        _BreakFrequency ("Break Frequency (윤곽선을 따라 끊기는 조각 개수)", Range(1, 20)) = 5
        _BreakAmount ("Break Amount (얼마나 많이 끊길지, 0이면 안 끊김)", Range(0, 1)) = 0.3
        _BreakSoftness ("Break Softness (끊긴 부분 경계 부드러움)", Range(0.01, 0.5)) = 0.15

        _WidthBumpInfluence ("Width Bump Influence (튀어나온 부분일수록 두껍게, 0이면 두께 균일)", Range(0, 1)) = 0.7
        _WidthSegmentCount ("Width Segment Count (두께가 구간별로 몇 조각으로 나뉘어 달라질지)", Range(2, 24)) = 10
        _WidthSegmentVariation ("Width Segment Variation (구간마다 두께 편차, 0이면 구간 구분 없음)", Range(0, 1)) = 0.6

        _Intensity ("Intensity (전체 밝기)", Range(1, 8)) = 3
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
        // 순수 가산(Additive) 블렌드 - 윤곽선만 밝게 튀고 안팎은 투명하게 비친다.
        Blend One One

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 col : COLOR;
            };

            float _Progress;
            float _FadeStart;
            float _GrowthPower;

            float4 _Color;

            float _MaxRadius;
            float _RingWidth;
            float _EdgeSoftness;
            float _BumpScale;
            float _BumpStrength;
            float _Seed;

            float _BreakFrequency;
            float _BreakAmount;
            float _BreakSoftness;

            float _WidthBumpInfluence;
            float _WidthSegmentCount;
            float _WidthSegmentVariation;

            float _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.col = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(cell);
                float b = hash21(cell + float2(1, 0));
                float c = hash21(cell + float2(0, 1));
                float d = hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 저주파 2옥타브 FBM - 세밀한 균열이 아니라 크고 둥근 뭉게구름 덩어리를 만드는 용도.
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.6;
                v += vnoise(p) * a; p *= 2.0; a *= 0.4;
                v += vnoise(p) * a;
                return v;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float r = length(p);
                float2 n = r > 0.00001 ? p / r : float2(0.0, 1.0);
                float angle = atan2(p.y, p.x);

                float progress = saturate(_Progress);

                // 즉시 나타났다가 FadeStart 이후 서서히 사라지는 봉투.
                float appear = smoothstep(0.0, 0.05, progress);
                float fadeOut = 1.0 - smoothstep(_FadeStart, 1.0, progress);
                float envelope = saturate(appear * fadeOut);

                // _GrowthPower로 반경이 커지는 속도(이징)를 조절한다 - 1이면 그대로 선형,
                // 1보다 작으면 초반에 빠르게 커졌다 점점 느려지고, 1보다 크면 초반엔 느리다가 막판에 확 커진다.
                float growthT = saturate(progress / max(0.001, _FadeStart));
                float baseRadius = _MaxRadius * pow(growthT, _GrowthPower);

                // 저주파 노이즈로 원의 경계를 뭉게구름처럼 둥글둥글하게 부풀린다 - 광선이나 균열이 아니라
                // 원 하나의 윤곽선 자체가 완만하게 울렁이는 형태.
                float bump = fbm(n * _BumpScale + float2(_Seed * 4.1, _Seed * 2.7));
                float ringRadius = baseRadius + (bump - 0.5) * _BumpStrength;

                // 같은 bump 값을 재활용해서, 튀어나온 구름 덩어리는 두껍게 들어간 부분은 얇게 - 부풀어 오른 정도랑
                // 두께가 서로 연동되니 독립적인 잡음보다 훨씬 뚜렷하게 보인다.
                float bumpWidthMul = lerp(1.0 - _WidthBumpInfluence, 1.0 + _WidthBumpInfluence, bump);

                // 여기에 더해 원을 구간(segment)으로 딱딱 나누고 구간마다 완전히 다른 두께를 랜덤하게 배정해서,
                // 매끈하게 이어지는 변화가 아니라 구간별로 뚜렷하게 굵기가 다르게 보이도록 한다.
                float widthSegCoord = (angle / 6.2831853 + 0.5) * _WidthSegmentCount;
                float widthSegId = floor(widthSegCoord);
                float segRandom = hash21(float2(widthSegId, _Seed * 7.7 + 3.3));
                float segWidthMul = lerp(1.0 - _WidthSegmentVariation, 1.0 + _WidthSegmentVariation, segRandom);

                float ringWidth = max(0.001, _RingWidth * bumpWidthMul * segWidthMul);

                // 윤곽선(테두리)만 밝게 - 안쪽도 바깥쪽도 채우지 않는다.
                float ringDist = abs(r - ringRadius);
                float ringMask = 1.0 - smoothstep(ringWidth * 0.5, ringWidth * 0.5 + _EdgeSoftness, ringDist);

                // 윤곽선을 따라 군데군데 랜덤하게 끊어서 완전히 이어진 원이 아니게 만든다.
                float2 breakCoord = n * _BreakFrequency + float2(_Seed * 13.3, _Seed * 9.7);
                float breakNoise = fbm(breakCoord);
                float breakMask = smoothstep(_BreakAmount, _BreakAmount + _BreakSoftness, breakNoise);
                ringMask *= breakMask;

                float alpha = saturate(ringMask) * envelope;

                half3 finalRgb = _Color.rgb * _Intensity * alpha * i.col.rgb;
                return half4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }
}
