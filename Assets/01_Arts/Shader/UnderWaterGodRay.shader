Shader "Effect/UnderwaterGodrayURP"
{
    Properties{
        _Color("Glow Color", Color) = (1,0.9,0.8,1)
        _Intensity("Glow Intensity", Range(0,2)) = 0.4
        _AlphaScale("Alpha Scale", Range(0,2)) = 1.0
        _Speed("Flow Speed", Range(0,2)) = 0.4
        _Freq1("Freq 1", Range(0.1,8)) = 1.0
        _Freq2("Freq 2", Range(0.1,8)) = 4.0
        _TopFadeIn("Top Fade In", Range(0,1)) = 0.05
        _TopFadeOut("Top Fade Out", Range(0,1)) = 0.96
        _YOffset("Polar Y Offset", Range(0,4)) = 2.0
        _PolarScale("Polar Scale", Range(1,100)) = 50.0
        _CenterX("Center X (0..1)", Range(0,1)) = 0.7
    }
    SubShader{
        // URP tag must be UniversalPipeline; UniversalRenderPipeline gets stripped in builds (renders magenta)
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        ZWrite Off Cull Off Blend One Zero

        Pass{
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _Color;
            float _Intensity, _AlphaScale, _Speed, _Freq1, _Freq2;
            float _TopFadeIn, _TopFadeOut, _YOffset, _PolarScale, _CenterX;

            // ???? ????? ?????? (C#???? Shader.SetGlobalFloat?? ????)
            float _CX_RT;
            float _YOFF_RT;

            struct VOut {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            VOut Vert(uint id : SV_VertexID){
                VOut o;
                float2 pos = (id==0)? float2(-1,-1) : (id==1)? float2(-1,3) : float2(3,-1);
                o.pos = float4(pos,0,1);
                o.uv = float2((pos.x+1)*0.5, (pos.y+1)*0.5);
                #if UNITY_UV_STARTS_AT_TOP
                    o.uv.y = 1.0 - o.uv.y;
                #endif
                return o;
            }

            float2 rhash(float2 uv){
                const float2x2 myt = float2x2(0.12121212, 0.13131313, -0.13131313, 0.12121212);
                const float2   mys = float2(1e4, 1e6);
                uv = mul(uv, myt);
                uv *= mys;
                return frac(frac(uv / mys) * uv);
            }

            float voronoi2d(float2 pnt){
                float2 p = floor(pnt);
                float2 f = frac(pnt);
                float res = 0.0;
                [unroll] for(int j=-1;j<=1;j++){
                    [unroll] for(int i=-1;i<=1;i++){
                        float2 b = float2(i,j);
                        float2 r = b - f + rhash(p + b);
                        res += 1.0 / pow(dot(r,r), 8.0);
                    }
                }
                return pow(1.0 / res, 0.0625);
            }

            float2 cart2polar(float2 xy){
                float phi = atan2(xy.y, xy.x);
                float r   = length(xy);
                return float2(phi, r);
            }

            float4 Frag(VOut i) : SV_Target
            {
                float2 iResolution = _ScaledScreenParams.xy;
                float2 uv = i.uv;

                float3 baseCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv).rgb;

                float2 p = uv;
                float aspect = iResolution.x / max(1.0, iResolution.y);
                p.x *= aspect;

                // CenterX, YOffset?? ????? ?????? ???
                float centerX = saturate(_CenterX + _CX_RT);
                float yOff    = _YOffset + _YOFF_RT;

                float2 v = (uv - float2(centerX, 0.5)) * 2.0;
                v.x *= aspect;
                v.y -= yOff;
                v /= _PolarScale;
                float2 polar = cart2polar(v);

                float t = _Time.y * _Speed;

                float n1 = voronoi2d( (float2(polar.x, 0.0) + 0.04 * t) * _Freq1 );
                float n2 = voronoi2d( (float2(0.1, polar.x) + 0.04 * t * 1.5) * _Freq2 );
                float n3 = min(n1, n2);

                float mask = smoothstep(_TopFadeIn, _TopFadeOut, p.y);
                float brightness = n3 * mask * _Intensity;
                float3 glow = _Color.rgb * brightness;

                return float4(baseCol + glow, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
