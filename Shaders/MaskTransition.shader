Shader "Ursa/MaskTransition"
{
    Properties
    {
        _Progress  ("Progress",  Range(0, 1))    = 0
        _Softness  ("Softness",  Range(0, 0.2)) = 0.05
        _MaskTex   ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            float      _Progress;
            float      _Softness;
            sampler2D  _MaskTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // マスクの輝度を閾値として使用。
                // 低い値のピクセルほど先に黒くなる（= Progressが低い段階で覆われる）。
                float mask  = tex2D(_MaskTex, i.uv).r;
                float alpha = smoothstep(mask - _Softness, mask + _Softness, _Progress);
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
