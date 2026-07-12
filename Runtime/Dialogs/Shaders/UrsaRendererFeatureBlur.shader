Shader "Ursa/UI/RendererFeatureBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 20)) = 4.0
        _OverlayColor ("Overlay Color", Color) = (0,0,0,0.3)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "URSA_RENDERER_FEATURE_BLUR"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_UrsaDialogBlurTexture);
            float4 _UrsaDialogBlurTexture_TexelSize;
            float _BlurSize;
            float4 _OverlayColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half3 SampleBlur(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_UrsaDialogBlurTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(uv)).rgb;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 offset = abs(_UrsaDialogBlurTexture_TexelSize.xy) * _BlurSize;

                half3 col = SampleBlur(uv) * 4.0;
                col += SampleBlur(uv + float2(offset.x, 0.0)) * 2.0;
                col += SampleBlur(uv - float2(offset.x, 0.0)) * 2.0;
                col += SampleBlur(uv + float2(0.0, offset.y)) * 2.0;
                col += SampleBlur(uv - float2(0.0, offset.y)) * 2.0;
                col += SampleBlur(uv + offset) * 1.0;
                col += SampleBlur(uv - offset) * 1.0;
                col += SampleBlur(uv + float2(offset.x, -offset.y)) * 1.0;
                col += SampleBlur(uv - float2(offset.x, -offset.y)) * 1.0;
                col /= 16.0;

                col = lerp(col, _OverlayColor.rgb, _OverlayColor.a);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
