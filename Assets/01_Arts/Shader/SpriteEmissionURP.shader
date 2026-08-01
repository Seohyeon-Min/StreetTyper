Shader "NewManzo/Sprite Emission (URP)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission Color (HDR)", Color) = (0, 0, 0, 1)
        _EmissionScale ("Emission Scale", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SpriteEmission"
            Tags { "LightMode" = "Universal2D"  }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _EmissionColor;
                half _EmissionScale;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                const VertexPositionInputs posIn = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posIn.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                const half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half4 tinted = tex * i.color * _Color;

                // 알파로 실루엣 밖은 발광 제한 (스프라이트 실루엣 기준)
                const half3 emission = _EmissionColor.rgb * _EmissionScale * tinted.a;
                const half3 rgb = tinted.rgb + emission;

                return half4(rgb, tinted.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
