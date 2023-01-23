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
            #include "Utils.hlsl"
            
            #pragma multi_compile_local FALLOFF_IMPACT _
            #pragma multi_compile_local INTENSITY_IMPACT _
            #pragma multi_compile_local FRAGMENT_RANDOM TEXTURE_RANDOM _

            #pragma vertex vert
            #pragma fragment frag

            sampler2D _ColorTex;
            sampler2D _DistTex;
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
            struct fragIn_gi
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
#if defined(FRAGMENT_RANDOM) || defined(TEXTURE_RANDOM)
                float2 noise_uv : TEXCOORD1;
#endif
            };

            // =======================================================================
            fragIn_gi vert(vertIn v)
            {
                fragIn_gi o;
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

                    uvPos += dir * tex2D(_DistTex, uvPos).rr / _Aspect;
                    if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                        return AMBIENT;
                }

                return AMBIENT;
            }

            float4 frag(fragIn_gi i) : SV_Target
            {
                float3 result = AMBIENT;

#if defined(FRAGMENT_RANDOM)
                const float rand = random(i.noise_uv);
#elif defined(TEXTURE_RANDOM)
				const float rand = tex2D(_NoiseTex, i.noise_uv).r * float(3.1415);
#else
				const float rand = 0;
#endif

                for (float f = 0.; f < _Samples; f++)
                {
                    const float t = (f + rand) / _Samples * float(3.1415 * 2.);
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
        
        Pass
        {
            name "GiBounce"

            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma multi_compile_local FALLOFF_IMPACT _
            #pragma multi_compile_local INTENSITY_IMPACT _
            #pragma multi_compile_local FRAGMENT_RANDOM TEXTURE_RANDOM _
            
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _ColorTex;
            sampler2D _AlphaTex;
            sampler2D _DistTex;
            sampler2D _NoiseTex;

			float4 _ColorTex_TexelSize;
            float  _Samples;
            float2 _Aspect;
            float2 _NoiseOffset;

            float _Falloff;
            float _Intensity;
            float _IntensityBounce;

            #define STEPS		16
            #define AMBIENT		float3(0, 0, 0)

            // =======================================================================
            struct fragIn_gi
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
#if defined(FRAGMENT_RANDOM) || defined(TEXTURE_RANDOM)
                float2 noise_uv : TEXCOORD1;
#endif
            };

            // =======================================================================
            fragIn_gi vert(vertIn v)
            {
                fragIn_gi o;
                o.vertex = v.vertex;
                o.uv = v.uv;

#if defined(FRAGMENT_RANDOM) || defined(TEXTURE_RANDOM)
                o.noise_uv = v.uv + _NoiseOffset;
#endif
                return o;
            }

            float3 trace(in const float2 uv, in const float2 dir)
            {
                float2 uvPos = uv + dir * _ColorTex_TexelSize.xy;
                if (tex2D(_AlphaTex, uvPos).r == 1)
                    return AMBIENT;

                [unroll]
                for (int n = 0; n < STEPS; n++)
                {
                    if (tex2D(_AlphaTex, uvPos).r == 1)
                    {
                        float3 result = tex2D(_ColorTex, uvPos).rgb;

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
            
            float4 frag(fragIn_gi i) : SV_Target
            {
                if (isOutlinePixel(_AlphaTex, i.uv, _ColorTex_TexelSize) == false)
                    discard;
                
                float3 result = AMBIENT;

#if defined(FRAGMENT_RANDOM)
                const float rand = random(i.noise_uv);
#elif defined(TEXTURE_RANDOM)
				const float rand = tex2D(_NoiseTex, i.noise_uv).r * float(3.1415);
#else
				const float rand = 0;
#endif
                for (float f = 0.; f < _Samples; f++)
                {
                    const float t = (f + rand) / _Samples * float(3.1415 * 2.);
                    result += trace(i.uv, float2(cos(t), sin(t)));
                }

#ifdef INTENSITY_IMPACT
                result *= _Intensity;
#endif
                result *= _IntensityBounce;
                result /= _Samples;
                

                return float4(result, 1);
            }
            ENDHLSL
        }
    }
}