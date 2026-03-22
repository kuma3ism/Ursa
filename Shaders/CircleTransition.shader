Shader "Ursa/CircleTransition"
{
    Properties
    {
        _Progress ("Progress", Range(0, 1)) = 0
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

            float _Progress;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 中心からの距離を 0〜1 に正規化（コーナーが 1.0）
                float dist = length(i.uv - 0.5) * 1.4142;
                float alpha = step(dist, _Progress);
                return fixed4(0, 0, 0, alpha);
            }
            ENDCG
        }
    }
}
