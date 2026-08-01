Shader "UI/UIStyle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Gauge Fill)]
        _FillAmount ("Fill Amount (왼쪽 고정, 오른쪽만 늘었다줄었다, 모서리 유지, 1=꽉 참)", Range(0, 1)) = 1

        [Header(Rounded Corners)]
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
        [Toggle] _UsePerCornerRadius ("Use Per-Corner Radius (모서리별 다르게)", Float) = 0
        _CornerRadiusTopLeft ("Corner Radius Top Left", Range(0, 0.5)) = 0.1
        _CornerRadiusTopRight ("Corner Radius Top Right", Range(0, 0.5)) = 0.1
        _CornerRadiusBottomLeft ("Corner Radius Bottom Left", Range(0, 0.5)) = 0.1
        _CornerRadiusBottomRight ("Corner Radius Bottom Right", Range(0, 0.5)) = 0.1
        _Skew ("Skew (아래쪽 고정, 위쪽이 좌우로 기울어짐 - 사다리꼴)", Range(-1, 1)) = 0
        [Toggle] _UseCapsuleShape ("Use Capsule/Pill Shape", Float) = 0
        [Toggle] _PillShapeHorizontal ("Pill Shape Horizontal", Float) = 0
        [Toggle] _PillShapeVertical ("Pill Shape Vertical", Float) = 0
        [Toggle] _UseDiamondShape ("Use Diamond/Rhombus Shape", Float) = 0
        _DiamondCurvature ("Diamond Edge Curvature", Range(0.2, 4)) = 1

        [Header(Edge Blur)]
        [Toggle] _EnableEdgeBlur ("Enable Edge Blur (Airbrush)", Float) = 0
        _EdgeBlurAmount ("Edge Blur Amount", Range(0, 0.5)) = 0.05

        [Header(Edge Fade)]
        [Toggle] _EnableEdgeFade ("Enable Edge Fade (Side Transparency)", Float) = 0
        [Toggle] _EdgeFadeVertical ("Fade Top/Bottom instead of Left/Right", Float) = 0
        _EdgeFadeWidth ("Edge Fade Width", Range(0, 0.5)) = 0.2
        
        [Header(Drop Shadow)]
        [Toggle] _EnableDropShadow ("Enable Drop Shadow", Float) = 0
        _DropShadowOffset ("Shadow Offset", Vector) = (2, -2, 0, 0)
        _DropShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
        _DropShadowBlur ("Shadow Blur", Range(0, 0.1)) = 0.02
        _DropShadowSize ("Shadow Size", Range(-0.5, 0.5)) = 0.0
        
        [Header(Inner Shadow)]
        [Toggle] _EnableInnerShadow ("Enable Inner Shadow", Float) = 0
        _InnerShadowOffset ("Inner Shadow Offset", Vector) = (0, 0, 0, 0)
        _InnerShadowColor ("Inner Shadow Color", Color) = (0, 0, 0, 0.3)
        _InnerShadowBlur ("Inner Shadow Blur", Range(0, 0.1)) = 0.02
        
        [Header(Gradient)]
        [Toggle] _EnableGradient ("Enable Gradient", Float) = 0
        _GradientBaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Space(5)]
        [Header(Color Gradient)]
        [Toggle] _EnableColorGradient ("Enable Color Gradient", Float) = 0
        _GradientColorStart ("Gradient Start Color", Color) = (1, 1, 1, 1)
        _GradientColorEnd ("Gradient End Color", Color) = (0.5, 0.5, 0.5, 1)
        [Toggle] _GradientRadial ("Radial (Center-based) Direction", Float) = 0
        [Range(0, 360)]
        _GradientDirection ("Gradient Direction", Float) = 270
        [Range(0, 1)]
        _GradientBlend ("Gradient Blend", Float) = 0.5
        [Space(5)]
        [Header(Light Gradient)]
        _LightGradientStrength ("Light Gradient Strength", Range(0, 0.1)) = 0.05
        _LightDirection ("Light Direction", Range(0, 360)) = 270
        [Space(5)]
        [Header(Hue Shift)]
        _HueShiftWarm ("Warm Hue Shift", Range(-10, 10)) = 2
        _HueShiftCool ("Cool Hue Shift", Range(-10, 10)) = -2
        [Space(5)]
        [Header(Edge Highlight)]
        _EdgeHighlightStrength ("Edge Highlight", Range(0, 0.2)) = 0.05
        _EdgeHighlightSize ("Edge Size", Range(0, 0.1)) = 0.02
        [Space(5)]
        [Header(Material)]
        _MaterialType ("Material Type", Range(0, 3)) = 0
        [Space(5)]
        [Header(Noise)]
        [Toggle] _EnableNoise ("Enable Micro Noise", Float) = 0
        _NoiseStrength ("Noise Strength", Range(0, 0.03)) = 0.01
        [Space(5)]
        [Header(Bottom Edge Line)]
        [Toggle] _EnableBottomEdgeLine ("Enable Bottom Edge Line", Float) = 0
        _EdgeLineThickness ("Edge Line Thickness", Range(0, 0.2)) = 0.02
        _EdgeLineIntensity ("Edge Line Intensity", Range(0, 1)) = 0.2
        _EdgeLineColor ("Edge Line Color", Color) = (0, 0, 0, 1)
        _EdgeLineSharpness ("Edge Line Sharpness", Range(0, 1)) = 0.5
        
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
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
            "RenderPipeline" = "UniversalPipeline"
        }
        
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                half4 color         : COLOR;
                float2 uv           : TEXCOORD0;
                float4 positionWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _TextureSampleAdd;
                float4 _ClipRect;
                float4 _MainTex_ST;
            
                // Gauge fill
                float _FillAmount;

                // Rounded corners
                float _CornerRadius;
                float _UsePerCornerRadius;
                float _CornerRadiusTopLeft;
                float _CornerRadiusTopRight;
                float _CornerRadiusBottomLeft;
                float _CornerRadiusBottomRight;
                float _UseCapsuleShape;
                float _PillShapeHorizontal;
                float _PillShapeVertical;
                float _UseDiamondShape;
                float _DiamondCurvature;
                float _Skew;

                // Edge blur (airbrush)
                float _EnableEdgeBlur;
                float _EdgeBlurAmount;

                // Edge fade (side transparency)
                float _EnableEdgeFade;
                float _EdgeFadeVertical;
                float _EdgeFadeWidth;
                
                // Drop shadow
                float _EnableDropShadow;
                float4 _DropShadowOffset;
                half4 _DropShadowColor;
                float _DropShadowBlur;
                float _DropShadowSize;
                
                // Inner shadow
                float _EnableInnerShadow;
                float4 _InnerShadowOffset;
                half4 _InnerShadowColor;
                float _InnerShadowBlur;
                
                // Gradient
                float _EnableGradient;
                half4 _GradientBaseColor;
                float _EnableColorGradient;
                half4 _GradientColorStart;
                half4 _GradientColorEnd;
                float _GradientRadial;
                float _GradientDirection;
                float _GradientBlend;
                float _LightGradientStrength;
                float _LightDirection;
                float _HueShiftWarm;
                float _HueShiftCool;
                float _EdgeHighlightStrength;
                float _EdgeHighlightSize;
                float _MaterialType;
                float _EnableNoise;
                float _NoiseStrength;
                
                // Bottom Edge Line
                float _EnableBottomEdgeLine;
                float _EdgeLineThickness;
                float _EdgeLineIntensity;
                half4 _EdgeLineColor;
                float _EdgeLineSharpness;
                
                // Aspect Ratio (실제 화면 비율)
                float _AspectRatio;
            CBUFFER_END
            
            // Rounded rectangle SDF
            float sdfRoundedRect(float2 p, float2 size, float radius)
            {
                // 반지름이 사각형 절반 크기보다 크면(예: 게이지가 얇아질 때) 공식이 뒤집혀 도형 전체가
                // "바깥"으로 판정되므로, 항상 절반 크기 이하로 고정한다.
                radius = min(radius, min(size.x, size.y));
                float2 q = abs(p) - size + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            // 모서리별로 다른 반지름을 줄 수 있는 사각형 SDF (p는 중심 기준 좌표).
            // radius = (top-right, bottom-right, top-left, bottom-left)
            float sdfRoundedRectPerCorner(float2 p, float2 size, float4 radius)
            {
                radius = min(radius, min(size.x, size.y));
                radius.xy = (p.x > 0.0) ? radius.xy : radius.zw;
                radius.x = (p.y > 0.0) ? radius.x : radius.y;
                float2 q = abs(p) - size + radius.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius.x;
            }
            
            // Capsule/Pill SDF (Horizontal)
            float sdfCapsuleHorizontal(float2 p, float2 size)
            {
                // 좌우 끝이 완전히 둥근 알약 형태
                // radius는 높이의 절반
                float radius = size.y * 0.5;
                float2 q = abs(p);
                
                // 부드럽게 연결되는 capsule SDF (조건문 없이)
                // 중앙 직사각형 부분에서 x를 클리핑하고, 원 부분까지의 거리 계산
                float2 capsuleCenter = float2(max(q.x - (size.x - radius), 0.0), q.y);
                return length(capsuleCenter) - radius;
            }
            
            // Capsule/Pill SDF (Vertical)
            float sdfCapsuleVertical(float2 p, float2 size)
            {
                // 위아래 끝이 완전히 둥근 알약 형태
                // radius는 너비의 절반
                float radius = size.x * 0.5;
                float2 q = abs(p);
                
                // 부드럽게 연결되는 capsule SDF (조건문 없이)
                // 중앙 직사각형 부분에서 y를 클리핑하고, 원 부분까지의 거리 계산
                float2 capsuleCenter = float2(q.x, max(q.y - (size.y - radius), 0.0));
                return length(capsuleCenter) - radius;
            }
            
            // Smart Capsule SDF (자동으로 가로/세로 판단)
            float sdfCapsule(float2 p, float2 size)
            {
                // 가로/세로 비율에 따라 자동 선택
                if (size.x > size.y)
                {
                    // 가로형: 좌우 끝이 둥글게
                    return sdfCapsuleHorizontal(p, size);
                }
                else
                {
                    // 세로형: 위아래 끝이 둥글게
                    return sdfCapsuleVertical(p, size);
                }
            }
            
            // Diamond/Rhombus SDF (b = 사각형의 절반 크기, 꼭짓점이 상/하/좌/우 중앙에 위치)
            // curvature == 1: 직선 변(완전한 마름모)
            // curvature  > 1: 변이 바깥으로 둥글게 휨 (원/타원 쪽으로)
            // curvature  < 1: 변이 안쪽으로 휘어 오목한 별 모양에 가까워짐
            float sdfDiamond(float2 p, float2 b, float curvature)
            {
                float2 q = abs(p) / max(b, 1e-5);
                float n = max(curvature, 0.05);
                float field = pow(q.x, n) + pow(q.y, n);
                float r = pow(max(field, 1e-8), 1.0 / n);
                return (r - 1.0) * min(b.x, b.y);
            }

            // Rotate UV
            float2 rotateUV(float2 uv, float angle)
            {
                float rad = angle * 3.14159265359 / 180.0;
                float s = sin(rad);
                float c = cos(rad);
                float2 centered = uv - 0.5;
                float2 rotated;
                rotated.x = centered.x * c - centered.y * s;
                rotated.y = centered.x * s + centered.y * c;
                return rotated + 0.5;
            }
            
            // RGB to HSV
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            
            // HSV to RGB
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }
            
            // Simple noise function
            float noise(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // Micro noise
            float microNoise(float2 uv)
            {
                float n = 0.0;
                n += noise(uv * 10.0) * 0.5;
                n += noise(uv * 20.0) * 0.25;
                n += noise(uv * 40.0) * 0.125;
                return (n - 0.5) * 2.0; // -1 to 1
            }
            
            // UnityGet2DClipping 함수 구현 (URP용)
            half UnityGet2DClipping(in float2 position, in float4 clipRect)
            {
                half2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionWS = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
               // 1. 비율 보정용 벡터 계산
                // 가로가 길면 x를 늘리고, 세로가 길면 y를 늘려 "정사각형 좌표계"를 만듭니다.
                float2 aspectVec = float2(max(1.0, _AspectRatio), max(1.0, 1.0 / _AspectRatio));

                // 2. UV를 중앙(-aspect, aspect)으로 정렬하고 스케일 조정
                // 기존 코드의 (* 2.0) 효과를 유지하기 위해 2.0을 곱합니다.
                float2 uvCentered = (IN.uv - 0.5) * 2.0 * aspectVec;

                // 3. 도형의 크기도 비율에 맞춰 확장
                // 기본 크기가 0.5라면, 늘어난 좌표계만큼 영역을 잡아줘야 꽉 찹니다.
                float2 rectSize = 0.5 * aspectVec;

                // 픽셀 스케일 계산 (안티에일리싱용 - 기존 로직 유지)
                float2 pixelSize = float2(ddx(IN.uv.x), ddy(IN.uv.y));
                float pixelScale = length(pixelSize * aspectVec) * 2.0;

                // 아래쪽(y = -rectSize.y) 고정, 위로 갈수록 x가 밀리는 스큐 - 도형을 사다리꼴처럼 기울인다.
                // 도형 SDF 계산에만 사용하고, 그래디언트/라이팅은 원래 uvCentered 기준을 유지한다.
                float2 skewedUvCentered = uvCentered;
                skewedUvCentered.x -= _Skew * (uvCentered.y + rectSize.y);

                // 모서리별 반지름(top-right, bottom-right, top-left, bottom-left)
                float4 cornerRadii = float4(_CornerRadiusTopRight, _CornerRadiusBottomRight, _CornerRadiusTopLeft, _CornerRadiusBottomLeft);

                // 게이지 채움: 왼쪽(-rectSize.x)은 고정하고 오른쪽 경계만 FillAmount에 따라 안쪽으로 들어온다.
                // 라운드 사각형(단일/모서리별) 브랜치에만 적용되며, FillAmount = 1이면 기존과 완전히 동일하다.
                float fillRightX = lerp(-rectSize.x, rectSize.x, saturate(_FillAmount));
                float2 fillRectSize = float2((fillRightX + rectSize.x) * 0.5, rectSize.y);
                float2 fillCenterOffset = float2((fillRightX - rectSize.x) * 0.5, 0.0);

                // 4. SDF 계산
                float sdf;
                if (_PillShapeHorizontal > 0.5)
                {
                    sdf = sdfCapsuleHorizontal(skewedUvCentered, rectSize);
                }
                else if (_PillShapeVertical > 0.5)
                {
                    sdf = sdfCapsuleVertical(skewedUvCentered, rectSize);
                }
                else if (_UseCapsuleShape > 0.5)
                {
                    sdf = sdfCapsule(skewedUvCentered, rectSize);
                }
                else if (_UseDiamondShape > 0.5)
                {
                    sdf = sdfDiamond(skewedUvCentered, rectSize, _DiamondCurvature);
                }
                else if (_UsePerCornerRadius > 0.5)
                {
                    sdf = sdfRoundedRectPerCorner(skewedUvCentered - fillCenterOffset, fillRectSize, cornerRadii);
                }
                else
                {
                    // 이제 _CornerRadius가 찌그러지지 않고 완벽한 원형으로 그려집니다.
                    sdf = sdfRoundedRect(skewedUvCentered - fillCenterOffset, fillRectSize, _CornerRadius);
                }
                
                // Main UI element color (텍스처 색상, 버튼 컬러 틴트는 나중에 적용)
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) + _TextureSampleAdd;
                half4 mainColor = texColor * IN.color;
                
                // ===== DROP SHADOW (드롭다운 셰도우) =====
                // 드롭 셰도우는 UI 요소 뒤에, offset만큼 아래/오른쪽으로 이동한 위치에 렌더링
                half4 finalColor = half4(0, 0, 0, 0);
                float dropShadowAlpha = 0.0;
                
                if (_EnableDropShadow > 0.5)
                {
                    // 픽셀 오프셋을 UV 공간으로 변환한다.
                    // 고정 1080 대신 실제 빌드 화면 높이를 사용해 해상도에 맞춘다.
                    // 드롭 셰도우는 아래/오른쪽으로 이동해야 함
                    // offset.y가 양수면 아래, offset.x가 양수면 오른쪽
                    float screenHeight = max(_ScreenParams.y, 1.0);
                    float2 shadowOffsetUV = float2(_DropShadowOffset.x, -_DropShadowOffset.y) / screenHeight;
                    // UV 공간에서 아래/오른쪽은 y 감소, x 증가
                    float2 shadowUV = skewedUvCentered - shadowOffsetUV;
                    
                    // 드롭 셰도우도 같은 shape 사용
                    float shadowSDF;
                    if (_PillShapeHorizontal > 0.5)
                    {
                        shadowSDF = sdfCapsuleHorizontal(shadowUV, rectSize);
                    }
                    else if (_PillShapeVertical > 0.5)
                    {
                        shadowSDF = sdfCapsuleVertical(shadowUV, rectSize);
                    }
                    else if (_UseCapsuleShape > 0.5)
                    {
                        shadowSDF = sdfCapsule(shadowUV, rectSize);
                    }
                    else if (_UseDiamondShape > 0.5)
                    {
                        shadowSDF = sdfDiamond(shadowUV, rectSize, _DiamondCurvature);
                    }
                    else if (_UsePerCornerRadius > 0.5)
                    {
                        shadowSDF = sdfRoundedRectPerCorner(shadowUV - fillCenterOffset, fillRectSize, cornerRadii);
                    }
                    else
                    {
                        shadowSDF = sdfRoundedRect(shadowUV - fillCenterOffset, fillRectSize, _CornerRadius);
                    }
                    
                    // 드롭 셰도우 알파: SDF 기반으로 부드러운 경계
                    // blur가 클수록 더 부드러운 그림자
                    // 픽셀 크기를 고려하여 일관된 그림자 품질 유지
                    float shadowEdgeWidth = _DropShadowBlur * 2.0 + pixelScale;
                    dropShadowAlpha = smoothstep(shadowEdgeWidth, -shadowEdgeWidth, shadowSDF);
                    dropShadowAlpha *= _DropShadowColor.a;
                    
                    // 드롭 셰도우 색상 (UI 요소와 겹치지 않는 부분만)
                    // 메인 UI 요소가 있는 위치에서는 그림자가 보이지 않도록
                    // 메인 UI 요소 마스크도 동일한 안티에일리싱 적용 (에어브러쉬 흐림 포함)
                    float mainEdgeWidth = max(pixelScale * 1.0, 0.001);
                    float mainSdfAlpha;
                    if (_EnableEdgeBlur > 0.5)
                    {
                        // 메인 요소도 바깥쪽으로만 퍼지므로, 셰도우와 겹치는 컷아웃 영역도 동일하게 확장
                        float mainBlurOuter = max(_EdgeBlurAmount, mainEdgeWidth);
                        mainSdfAlpha = smoothstep(mainBlurOuter, -mainEdgeWidth, sdf);
                    }
                    else
                    {
                        mainSdfAlpha = smoothstep(mainEdgeWidth, -mainEdgeWidth, sdf);
                    }
                    dropShadowAlpha *= (1.0 - mainSdfAlpha);
                    
                    // 드롭 셰도우 색상 적용
                    finalColor.rgb = _DropShadowColor.rgb;
                    finalColor.a = dropShadowAlpha;
                }
                
                // ===== MAIN UI ELEMENT =====
                // 메인 UI 요소의 알파 마스크 (SDF 기반)
                // 픽셀 크기에 맞춘 적절한 안티에일리싱 (선명하면서도 부드러움)
                float edgeWidth = max(pixelScale * 1.0, 0.001); // 최소값 보장
                float sdfAlpha;
                if (_EnableEdgeBlur > 0.5)
                {
                    // 에어브러쉬: 도형 내부는 그대로 유지하고, 가장자리 바깥쪽으로만 알파가 퍼지듯 흐려짐 (안쪽으로 줄어들지 않음)
                    float blurOuter = max(_EdgeBlurAmount, edgeWidth);
                    sdfAlpha = smoothstep(blurOuter, -edgeWidth, sdf);
                }
                else
                {
                    sdfAlpha = smoothstep(edgeWidth, -edgeWidth, sdf);
                }
                
                // 이미지의 실제 알파를 보존 (이미지가 있을 경우)
                // mainColor.a는 이미 _MainTex의 알파와 IN.color의 알파가 곱해진 값
                float mainAlpha = sdfAlpha * mainColor.a; // SDF 마스크와 이미지 알파를 결합
                
                // ===== DETAILED GRADIENT (세부 그래디언트) =====
                if (_EnableGradient > 0.5)
                {
                    half3 baseColor = _GradientBaseColor.rgb;
                    
                    // 0. Color Gradient (색상 그래디언트): 시작 색상에서 끝 색상으로
                    if (_EnableColorGradient > 0.5)
                    {
                        float gradientFactor;
                        if (_GradientRadial > 0.5)
                        {
                            // 중앙 기준 방사형 그래디언트: 중심(0) -> 가장자리(1)
                            float radialDist = length(uvCentered / max(rectSize, 1e-5));
                            gradientFactor = saturate(radialDist);
                        }
                        else
                        {
                            float gradientAngle = _GradientDirection * 3.14159265359 / 180.0;
                            float2 gradientDir = float2(sin(gradientAngle), cos(gradientAngle));
                            float gradientDot = dot(uvCentered, gradientDir);
                            // -1 to 1 범위를 0 to 1로 변환
                            gradientFactor = (gradientDot + 1.0) * 0.5;
                        }
                        // Blend 값으로 그래디언트 강도 조절
                        gradientFactor = lerp(0.5, gradientFactor, _GradientBlend);
                        // 시작 색상과 끝 색상 사이 보간
                        half3 colorGradient = lerp(_GradientColorStart.rgb, _GradientColorEnd.rgb, gradientFactor);
                        // Base Color와 색상 그래디언트 혼합
                        baseColor = lerp(baseColor, colorGradient, 1.0);
                    }
                    
                    // 1. Light Gradient (명암 그래디언트): 위→밝음, 아래→어두움
                    float lightAngle = _LightDirection * 3.14159265359 / 180.0;
                    float2 lightDir = float2(sin(lightAngle), cos(lightAngle));
                    float lightDot = dot(uvCentered, lightDir);
                    // -1 (어두움) to 1 (밝음)
                    float lightGradient = lightDot * _LightGradientStrength;
                    
                    // 밝기 조절: 100% → 92~95%
                    float brightness = 1.0 + lightGradient;
                    brightness = clamp(brightness, 0.92, 1.0);
                    baseColor *= brightness;
                    
                    // 2. Hue Shift (색온도 변화): 하이라이트는 따뜻하게, 섀도우는 차갑게
                    float3 hsv = rgb2hsv(baseColor);
                    float hueShift = lerp(_HueShiftCool, _HueShiftWarm, (lightDot + 1.0) * 0.5);
                    hsv.x += hueShift / 360.0; // Hue shift in degrees
                    if (hsv.x > 1.0) hsv.x -= 1.0;
                    if (hsv.x < 0.0) hsv.x += 1.0;
                    baseColor = hsv2rgb(hsv);
                    
                    // 3. Edge Highlight (엣지 하이라이트): 가장자리 밝기 변화
                    float edgeDist = abs(sdf);
                    float edgeMask = smoothstep(_EdgeHighlightSize * 2.0, 0.0, edgeDist);
                    edgeMask *= smoothstep(0.0, _EdgeHighlightSize, edgeDist);
                    baseColor += edgeMask * _EdgeHighlightStrength;
                    
                    // 4. Material Type (재질 그래디언트)
                    if (_MaterialType < 0.5) // Plastic
                    {
                        // 부드러운 전이
                        baseColor = lerp(baseColor, baseColor * 1.1, lightGradient * 0.3);
                    }
                    else if (_MaterialType < 1.5) // Metal
                    {
                        // 대비 강한 명암
                        float metalContrast = pow(abs(lightGradient), 0.5);
                        baseColor = lerp(baseColor * 0.85, baseColor * 1.15, metalContrast);
                    }
                    else if (_MaterialType < 2.5) // Glass
                    {
                        // 중심 밝음 + 엣지 어두움
                        float centerDist = length(uvCentered);
                        float glassGradient = 1.0 - smoothstep(0.0, 0.5, centerDist);
                        baseColor = lerp(baseColor * 0.9, baseColor * 1.1, glassGradient);
                    }
                    // Paper: 거의 변화 없음 (기본값 유지)
                    
                    // 5. Micro Noise (미세 노이즈)
                    if (_EnableNoise > 0.5)
                    {
                        float noiseValue = microNoise(IN.uv) * _NoiseStrength;
                        baseColor += noiseValue;
                    }
                    
                    // 최종 색상 적용 (Button의 색상 틴트도 반영)
                    // baseColor에 텍스처 색상과 버튼 컬러 틴트를 모두 적용
                    // 텍스처가 있으면 텍스처 색상과 블렌딩, 없으면 baseColor만 사용
                    half3 finalBaseColor = lerp(baseColor, baseColor * texColor.rgb, step(0.001, texColor.a));
                    mainColor.rgb = finalBaseColor * IN.color.rgb;
                }
                else
                {
                    // 그래디언트가 비활성화되어 있으면 Button 색상 틴트가 이미 mainColor에 적용됨
                    // (mainColor = (SAMPLE_TEXTURE2D(_MainTex, ...) + _TextureSampleAdd) * IN.color)
                }
                
                // Inner shadow 적용
                if (_EnableInnerShadow > 0.5)
                {
                    float2 innerOffsetUV = _InnerShadowOffset.xy / max(_ScreenParams.y, 1.0);
                    float2 innerUV = skewedUvCentered + innerOffsetUV;
                    
                    // Inner shadow도 같은 shape 사용
                    float innerSDF;
                    if (_PillShapeHorizontal > 0.5)
                    {
                        innerSDF = sdfCapsuleHorizontal(innerUV, rectSize);
                    }
                    else if (_PillShapeVertical > 0.5)
                    {
                        innerSDF = sdfCapsuleVertical(innerUV, rectSize);
                    }
                    else if (_UseCapsuleShape > 0.5)
                    {
                        innerSDF = sdfCapsule(innerUV, rectSize);
                    }
                    else if (_UseDiamondShape > 0.5)
                    {
                        innerSDF = sdfDiamond(innerUV, rectSize, _DiamondCurvature);
                    }
                    else if (_UsePerCornerRadius > 0.5)
                    {
                        innerSDF = sdfRoundedRectPerCorner(innerUV - fillCenterOffset, fillRectSize, cornerRadii);
                    }
                    else
                    {
                        innerSDF = sdfRoundedRect(innerUV - fillCenterOffset, fillRectSize, _CornerRadius);
                    }
                    
                    if (innerSDF < 0.0)
                    {
                        // 픽셀 크기를 고려하여 일관된 내부 그림자 품질 유지
                        float innerShadowEdgeWidth = _InnerShadowBlur * 2.0 + pixelScale;
                        float innerShadowAlpha = smoothstep(-innerShadowEdgeWidth, innerShadowEdgeWidth, -innerSDF);
                        innerShadowAlpha *= _InnerShadowColor.a;
                        mainColor.rgb = lerp(mainColor.rgb, _InnerShadowColor.rgb, innerShadowAlpha);
                    }
                }
                
                // ===== BOTTOM EDGE LINE (아래 엣지 라인) =====
                if (_EnableBottomEdgeLine > 0.5)
                {
                    // UV 좌표: uvCentered는 -1~1 범위, y가 작을수록 아래쪽
                    float yPos = uvCentered.y; // -1 (아래) ~ 1 (위)
                    
                    // SDF를 사용하여 가장자리에서의 거리 계산
                    float edgeDistance = abs(sdf);
                    
                    // 아래쪽 엣지만 감지 (y가 작을수록, 즉 아래쪽일수록)
                    // y가 -1에 가까울수록(아래쪽) 라인 표시
                    float bottomFactor = smoothstep(0.3, -1.0, yPos); // 아래쪽일수록 1에 가까움
                    
                    // 경계 뚜렷함에 따라 전환 방식 변경
                    // Sharpness가 낮을 때: 부드러운 smoothstep
                    // Sharpness가 높을 때: 뚜렷한 step (툰 셰이더처럼)
                    float edgeLineMask;
                    if (_EdgeLineSharpness < 0.1)
                    {
                        // 매우 부드러운 전환
                        edgeLineMask = smoothstep(_EdgeLineThickness * 2.0, 0.0, edgeDistance);
                    }
                    else if (_EdgeLineSharpness > 0.9)
                    {
                        // 매우 뚜렷한 전환 (step처럼)
                        edgeLineMask = step(edgeDistance, _EdgeLineThickness);
                    }
                    else
                    {
                        // 중간: smoothstep 범위를 좁혀서 뚜렷하게
                        float smoothRange = lerp(2.0, 0.01, _EdgeLineSharpness);
                        edgeLineMask = smoothstep(_EdgeLineThickness * smoothRange, _EdgeLineThickness * 0.01, edgeDistance);
                        // pow를 사용해서 더 뚜렷하게
                        edgeLineMask = pow(edgeLineMask, lerp(1.0, 10.0, _EdgeLineSharpness));
                    }
                    
                    edgeLineMask *= bottomFactor; // 아래쪽만
                    
                    // 라인 강도 적용
                    float lineIntensity = _EdgeLineIntensity * edgeLineMask;
                    
                    // 라인 색상 적용 (기본 색상과 라인 색상을 블렌딩)
                    mainColor.rgb = lerp(mainColor.rgb, _EdgeLineColor.rgb, lineIntensity * _EdgeLineColor.a);
                }
                
                // 메인 UI 요소를 드롭 셰도우 위에 합성
                // 드롭 셰도우가 있으면 먼저 그린 후, 메인 요소를 위에 올림
                if (_EnableDropShadow > 0.5)
                {
                    // Alpha blending: shadow + main
                    finalColor.rgb = lerp(finalColor.rgb, mainColor.rgb, mainAlpha);
                    finalColor.a = max(dropShadowAlpha, mainAlpha * mainColor.a);
                }
                else
                {
                    finalColor = mainColor;
                    finalColor.a *= mainAlpha;
                }
                
                // ===== EDGE FADE (좌우 또는 위아래 양 끝 동시 페이드아웃) =====
                if (_EnableEdgeFade > 0.5)
                {
                    // uvCentered/rectSize는 중심 기준 대칭이므로 abs(coord)/size가 0(중심)~1(가장자리)로 양쪽에 동일하게 적용됨
                    float axisCoord = (_EdgeFadeVertical > 0.5) ? uvCentered.y : uvCentered.x;
                    float axisSize = (_EdgeFadeVertical > 0.5) ? rectSize.y : rectSize.x;
                    float t = abs(axisCoord) / max(axisSize, 1e-5);
                    float fadeStart = 1.0 - _EdgeFadeWidth;
                    float edgeFade = 1.0 - smoothstep(fadeStart, 1.0, t);
                    finalColor.a *= edgeFade;
                }

                // 게이지가 거의 다 비었을 때(0.01 이하)는 드롭섀도우 등 아무것도 안 남기고 완전히 숨긴다.
                finalColor.a *= step(0.01, _FillAmount);

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.positionWS.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
