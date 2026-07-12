Shader "Ursa/UI/DialogBlur"
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

        GrabPass
        {
            "_GrabTexture"
        }

        Pass
        {
            Name "GAUSSIAN_BLUR"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;
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
                float4 grabPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float2 offset = _GrabTexture_TexelSize.xy * _BlurSize;

                // 9-tap Gaussian blur
                fixed4 col = tex2D(_GrabTexture, uv) * 4.0;
                col += tex2D(_GrabTexture, uv + float2(offset.x, 0.0)) * 2.0;
                col += tex2D(_GrabTexture, uv - float2(offset.x, 0.0)) * 2.0;
                col += tex2D(_GrabTexture, uv + float2(0.0, offset.y)) * 2.0;
                col += tex2D(_GrabTexture, uv - float2(0.0, offset.y)) * 2.0;
                col += tex2D(_GrabTexture, uv + offset) * 1.0;
                col += tex2D(_GrabTexture, uv - offset) * 1.0;
                col += tex2D(_GrabTexture, uv + float2(offset.x, -offset.y)) * 1.0;
                col += tex2D(_GrabTexture, uv - float2(offset.x, -offset.y)) * 1.0;
                col /= 16.0;

                // オーバーレイ色を乗算（濃さ調整）
                col.rgb = lerp(col.rgb, _OverlayColor.rgb, _OverlayColor.a);
                col.a = 1.0;

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
