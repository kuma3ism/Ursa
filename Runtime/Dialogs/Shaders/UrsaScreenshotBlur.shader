Shader "Ursa/UI/ScreenshotBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Range(0, 10)) = 2.0
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
            Name "GAUSSIAN_BLUR_HORIZONTAL"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 offset = float2(_MainTex_TexelSize.x * _BlurSize, 0.0);

                fixed4 col = tex2D(_MainTex, i.uv) * 4.0;
                col += tex2D(_MainTex, i.uv + offset) * 2.0;
                col += tex2D(_MainTex, i.uv - offset) * 2.0;
                col += tex2D(_MainTex, i.uv + offset * 2.0) * 1.0;
                col += tex2D(_MainTex, i.uv - offset * 2.0) * 1.0;
                col /= 10.0;

                col.rgb = lerp(col.rgb, _OverlayColor.rgb, _OverlayColor.a);
                col.a = 1.0;

                return col;
            }
            ENDCG
        }

        Pass
        {
            Name "GAUSSIAN_BLUR_VERTICAL"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 offset = float2(0.0, _MainTex_TexelSize.y * _BlurSize);

                fixed4 col = tex2D(_MainTex, i.uv) * 4.0;
                col += tex2D(_MainTex, i.uv + offset) * 2.0;
                col += tex2D(_MainTex, i.uv - offset) * 2.0;
                col += tex2D(_MainTex, i.uv + offset * 2.0) * 1.0;
                col += tex2D(_MainTex, i.uv - offset * 2.0) * 1.0;
                col /= 10.0;

                col.rgb = lerp(col.rgb, _OverlayColor.rgb, _OverlayColor.a);
                col.a = 1.0;

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
