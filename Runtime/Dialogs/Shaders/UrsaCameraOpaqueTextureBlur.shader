Shader "Ursa/UI/CameraOpaqueTextureBlur"
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
            Name "CAMERA_OPAQUE_TEXTURE_BLUR"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float2 offset = abs(_CameraOpaqueTexture_TexelSize.xy) * _BlurSize;

                half3 col = SampleSceneColor(uv) * 4.0;
                col += SampleSceneColor(uv + float2(offset.x, 0.0)) * 2.0;
                col += SampleSceneColor(uv - float2(offset.x, 0.0)) * 2.0;
                col += SampleSceneColor(uv + float2(0.0, offset.y)) * 2.0;
                col += SampleSceneColor(uv - float2(0.0, offset.y)) * 2.0;
                col += SampleSceneColor(uv + offset) * 1.0;
                col += SampleSceneColor(uv - offset) * 1.0;
                col += SampleSceneColor(uv + float2(offset.x, -offset.y)) * 1.0;
                col += SampleSceneColor(uv - float2(offset.x, -offset.y)) * 1.0;
                col /= 16.0;

                col = lerp(col, _OverlayColor.rgb, _OverlayColor.a);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
