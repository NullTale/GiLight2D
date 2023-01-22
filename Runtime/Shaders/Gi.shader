Shader "Hidden/GiLight2D/Gi"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            name "Gi"

            HLSLPROGRAM
            #pragma multi_compile_local FALLOFF_IMPACT _
            #pragma multi_compile_local INTENSITY_IMPACT _
            #pragma multi_compile_local FRAGMENT_RANDOM TEXTURE_RANDOM _

            #pragma vertex vert
            #pragma fragment frag

            #include <UnityShaderVariables.cginc>

            sampler2D _DistTex;
            sampler2D _ColorTex;
            sampler2D _NoiseTex;

            float _Samples;
            float2 _Aspect;
            float4 _Scale;
            float2 _NoiseOffset;

            float _Falloff;
            float _Intensity;

            #define STEPS		16
            #define AMBIENT		float3(0, 0, 0)

            // =======================================================================
            struct vertIn
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct fragIn
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
#if defined(FRAGMENT_RANDOM) || defined(TEXTURE_RANDOM)
                float2 noise_uv : TEXCOORD1;
#endif
            };

            // =======================================================================
            fragIn vert(vertIn v)
            {
                fragIn o;
                o.vertex = v.vertex * _Scale;
                o.uv = v.uv;

#if defined(FRAGMENT_RANDOM) || defined(TEXTURE_RANDOM)
                o.noise_uv = v.uv + _NoiseOffset;
#endif
                return o;
            }

            float3 trace(const float2 uv, const float2 dir)
            {
                float2 uvPos = uv;

                [unroll]
                for (int n = 0; n < STEPS; n++)
                {
                    const float4 col = tex2D(_ColorTex, uvPos).rgba;
                    if (col.a == 1)
                    {
                        float3 result = col.rgb;

                        #ifdef FALLOFF_IMPACT
                        result *= (1 + distance(uv, uvPos)) / _Falloff;
                        #endif

                        return result;
                    }

                    uvPos += dir * tex2D(_DistTex, uvPos).r / _Aspect;
                    if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                        return AMBIENT;
                }

                return AMBIENT;
            }

            float _random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float4 frag(fragIn i) : SV_Target
            {
                float3 result = AMBIENT;

#if defined(FRAGMENT_RANDOM)
                const float rand = _random(i.noise_uv);
#elif defined(TEXTURE_RANDOM)
				const float rand = tex2D(_NoiseTex, i.noise_uv).r * 3.1415;
#else
				const float rand = 0;
#endif

                for (float f = 0.; f < _Samples; f++)
                {
                    const float t = (f + rand) / _Samples * (3.1415 * 2.);
                    result += trace(i.uv, float2(cos(t), sin(t)));
                }

#ifdef INTENSITY_IMPACT
                result *= _Intensity;
#endif
                result /= _Samples;

                return float4(result, 1);
            }
            ENDHLSL
        }

    }
}