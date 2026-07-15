Shader "Ursa/UI/TapRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _RingThickness ("Ring Thickness", Range(0.01, 0.25)) = 0.065
        _EdgeSoftness ("Edge Softness", Range(0.0005, 0.03)) = 0.004
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float _RingThickness;
            float _EdgeSoftness;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float distanceFromCenter = length(input.uv - 0.5);
                float edge = max(fwidth(distanceFromCenter), _EdgeSoftness);
                float ringDistance = abs(distanceFromCenter - (0.5 - _RingThickness * 0.5));
                float ring = 1.0 - smoothstep(_RingThickness * 0.5, _RingThickness * 0.5 + edge, ringDistance);

                fixed4 color = input.color;
                color.a *= ring;
                return color;
            }
            ENDCG
        }
    }
}
