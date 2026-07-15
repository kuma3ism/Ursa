Shader "Hidden/Ursa/TapRipple"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Ursa Tap Ripple"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define URSA_MAX_RIPPLES 8

            int _UrsaRippleCount;
            float _UrsaRippleAspect;
            float4 _UrsaRippleCenters[URSA_MAX_RIPPLES];
            float4 _UrsaRippleParameters[URSA_MAX_RIPPLES];

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float2 displacement = 0.0;

                [unroll]
                for (int index = 0; index < URSA_MAX_RIPPLES; index++)
                {
                    if (index >= _UrsaRippleCount)
                        break;

                    float4 centerData = _UrsaRippleCenters[index];
                    float2 delta = uv - centerData.xy;
                    float2 correctedDelta = float2(delta.x * _UrsaRippleAspect, delta.y);
                    float distanceFromCenter = max(length(correctedDelta), 0.00001);
                    float width = max(centerData.w, 0.0005);
                    float normalizedDistance = (distanceFromCenter - centerData.z) / width;
                    float envelope = exp(-normalizedDistance * normalizedDistance * 2.0);
                    float wave = cos(normalizedDistance * PI) * envelope;
                    float2 direction = correctedDelta / distanceFromCenter;
                    direction.x /= _UrsaRippleAspect;
                    displacement += direction * wave * _UrsaRippleParameters[index].x;
                }

                float2 sampleUv = saturate(uv + displacement);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUv, _BlitMipLevel);
            }
            ENDHLSL
        }
    }
}
