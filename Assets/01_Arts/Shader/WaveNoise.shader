Shader "UI/WaveNoise"
{
    Properties
    {
        _Speed ("Speed", Range(0, 5)) = 0.1
        _MainColor ("Main Color", Color) = (0.95, 0.94, 0.92, 1.0)
        _GradientColor ("Gradient Color", Color) = (0.85, 0.88, 0.90, 1.0)
        _Scale ("Scale", Range(0.0005, 0.01)) = 0.002
        _DistortionStrength ("Distortion Strength", Range(0.1, 1.0)) = 0.3
        _ColorBlend ("Color Blend", Range(0.1, 3.0)) = 1.5
        _Randomness ("Randomness", Range(0, 2.0)) = 1.0
        _LayerCount ("Layer Count", Range(1, 5)) = 3
        [Toggle] _UseUnscaledTime ("Use Unscaled Time (일시정지 중에도 계속 움직임)", Float) = 0

        // UI(Image)에 물릴 때만 의미 있는 값들. UIStyle.shader와 같은 패턴이다 - uGUI가
        // RectMask2D/Mask 밑에서 이 프로퍼티들을 런타임에 직접 채워 넣는다.
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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        // ⚠️ 이 넷(Stencil/Cull Off/ZWrite Off/Blend)이 빠져 있으면 UI에서는 안 보인다.
        // 원래 이 셰이더는 3D 오브젝트용(Opaque, Cull Back, ZWrite On)으로 짜여 있었는데,
        // uGUI 캔버스는 Cull Back이면 감기는 방향에 따라 통째로 컬링되고, Stencil이 없으면
        // RectMask2D/Mask 밑에서 마스킹에 안 걸려 아예 안 그려질 수 있다(UIStyle.shader와
        // 같은 이유 - 그쪽은 처음부터 UI용으로 이 블록들을 갖고 있었다).
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Speed;
                float4 _MainColor;
                float4 _GradientColor;
                float _Scale;
                float _DistortionStrength;
                float _ColorBlend;
                float _Randomness;
                float _LayerCount;
                float _UseUnscaledTime;
                float4 _ClipRect;
            CBUFFER_END

            // ⚠️ UnityPerMaterial 밖에 둔다 - 이건 머티리얼마다 다른 값이 아니라 "지금 실제
            // 경과 시간이 얼마인가" 하나뿐인 진짜 전역값이다(UIStyle.shader의 머티리얼별
            // 프로퍼티들과 다르다). Shader.SetGlobalFloat로 매 프레임 채워주는 쪽(스크립트)이
            // 하나만 있으면 이 셰이더를 쓰는 모든 머티리얼이 같이 갱신된다.
            float _GlobalUnscaledTime;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.worldPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            
            // 간단한 랜덤 함수
            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // URP엔 내장 UnityGet2DClipping이 없어 UIStyle.shader와 같은 구현을 그대로 둔다.
            half UnityGet2DClipping(in float2 position, in float4 clipRect)
            {
                half2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                // 화면 좌표로 변환
                float2 fragCoord = input.uv * _ScreenParams.xy;
                
                float speed = _Speed;
                float scale = _Scale;
                // _Time.y는 Time.timeScale을 그대로 타므로 일시정지(timeScale=0) 중엔 멎는다.
                // 계속 움직여야 하면(_UseUnscaledTime) UnscaledShaderTime.cs가 매 프레임 채워주는
                // 전역값을 대신 쓴다.
                float t = _UseUnscaledTime > 0.5 ? _GlobalUnscaledTime : _Time.y;
                
                float totalPattern = 0.0;
                float totalWeight = 0.0;
                
                // 여러 레이어를 합쳐서 더 랜덤하고 블렌드된 효과
                int layerCount = (int)_LayerCount;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    float layerFloat = float(layer);
                    
                    // 각 레이어마다 랜덤 오프셋과 스케일
                    float2 layerOffset = float2(
                        sin(layerFloat * 7.3 + t * 0.3) * _Randomness,
                        cos(layerFloat * 5.7 - t * 0.4) * _Randomness
                    );
                    float layerScale = scale * (0.8 + sin(layerFloat * 3.1) * 0.4);
                    float layerSpeed = speed * (0.7 + cos(layerFloat * 2.3) * 0.3);
                    
                    float2 p = (fragCoord + layerOffset) * layerScale;
                    
                    // 반복문으로 좌표 왜곡 (각 레이어마다 다른 파라미터)
                    for (int i = 1; i < 10; i++)
                    {
                        float iFloat = float(i);
                        float randomPhase = sin(layerFloat * 11.7 + iFloat * 3.5) * _Randomness;
                        float freqMultiplier = 2.5 + sin(layerFloat * 4.2) * 1.5;
                        
                        p.x += _DistortionStrength / iFloat * sin(iFloat * freqMultiplier * p.y + t * layerSpeed + randomPhase);
                        p.y += _DistortionStrength / iFloat * cos(iFloat * freqMultiplier * p.x + t * layerSpeed * 1.1 - randomPhase);
                    }
                    
                    // 왜곡된 좌표로 패턴 생성 (여러 패턴 혼합)
                    float pattern1 = cos(p.x + p.y + layerFloat * 2.1 + t * 0.2) * 0.5 + 0.5;
                    float pattern2 = sin(p.x * 1.3 - p.y * 0.7 + layerFloat * 1.7 - t * 0.15) * 0.5 + 0.5;
                    float pattern3 = cos((p.x - p.y) * 0.9 + layerFloat * 3.3 + t * 0.1) * 0.5 + 0.5;
                    
                    float layerPattern = (pattern1 * 0.5 + pattern2 * 0.3 + pattern3 * 0.2);
                    
                    // 레이어 가중치 (중앙 레이어가 더 강하게)
                    float weight = 1.0 / (1.0 + abs(layerFloat - float(layerCount) * 0.5) * 0.5);
                    
                    totalPattern += layerPattern * weight;
                    totalWeight += weight;
                }
                
                // 패턴 정규화
                float pattern = totalPattern / totalWeight;
                
                // 매우 부드러운 색상 전환 (경계가 완전히 풀어지게)
                float blendRange = _ColorBlend * 0.3;
                pattern = smoothstep(0.5 - blendRange, 0.5 + blendRange, pattern);
                
                // 추가 부드러움 (더블 블렌드)
                pattern = smoothstep(0.0, 1.0, pattern);
                
                // 두 색을 매우 부드럽게 섞기
                float3 finalColor = lerp(_MainColor.rgb, _GradientColor.rgb, pattern);

                // ⚠️ 알파를 무조건 1로 고정하면 안 된다 - CanvasGroup으로 페이드하는 패널
                // 배경에 이 셰이더를 쓰면(예: CardCollectionPanel) 알파가 vertex color를 통해
                // 내려오는데, 여기서 무시하면 페이드가 하나도 안 보인다.
                float4 color = float4(finalColor * input.color.rgb, _MainColor.a * input.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
