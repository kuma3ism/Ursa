Shader "Ursa/ScreenshotBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        // Pass 0: 水平ブラー
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_horizontal
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag_horizontal(v2f i) : SV_Target
            {
                float2 texel = float2(_MainTex_TexelSize.x * _BlurSize, 0);
                fixed4 col = tex2D(_MainTex, i.uv) * 0.227;
                col += tex2D(_MainTex, i.uv + texel * 1.0) * 0.194;
                col += tex2D(_MainTex, i.uv - texel * 1.0) * 0.194;
                col += tex2D(_MainTex, i.uv + texel * 2.0) * 0.121;
                col += tex2D(_MainTex, i.uv - texel * 2.0) * 0.121;
                col += tex2D(_MainTex, i.uv + texel * 3.0) * 0.054;
                col += tex2D(_MainTex, i.uv - texel * 3.0) * 0.054;
                col += tex2D(_MainTex, i.uv + texel * 4.0) * 0.016;
                col += tex2D(_MainTex, i.uv - texel * 4.0) * 0.016;
                return col;
            }
            ENDCG
        }

        // Pass 1: 垂直ブラー
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_vertical
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurSize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag_vertical(v2f i) : SV_Target
            {
                float2 texel = float2(0, _MainTex_TexelSize.y * _BlurSize);
                fixed4 col = tex2D(_MainTex, i.uv) * 0.227;
                col += tex2D(_MainTex, i.uv + texel * 1.0) * 0.194;
                col += tex2D(_MainTex, i.uv - texel * 1.0) * 0.194;
                col += tex2D(_MainTex, i.uv + texel * 2.0) * 0.121;
                col += tex2D(_MainTex, i.uv - texel * 2.0) * 0.121;
                col += tex2D(_MainTex, i.uv + texel * 3.0) * 0.054;
                col += tex2D(_MainTex, i.uv - texel * 3.0) * 0.054;
                col += tex2D(_MainTex, i.uv + texel * 4.0) * 0.016;
                col += tex2D(_MainTex, i.uv - texel * 4.0) * 0.016;
                return col;
            }
            ENDCG
        }
    }
}
