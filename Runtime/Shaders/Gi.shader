Shader "Hidden/GiLight2D/Gi"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass    // 0
        {
            name "Gi"

            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma multi_compile_local FALLOFF_IMPACT _
            #pragma multi_compile_local INTENSITY_IMPACT _
            #pragma multi_compile_local RAY_BOUNCES _
            #pragma multi_compile_local FRAGMENT_RANDOM TEXTURE_RANDOM _

            #pragma vertex vert
            #pragma fragment frag

            sampler2D _ColorTex;
            sampler2D _BounceTex;
            sampler2D _DistTex;
            sampler2D _NoiseTex;

            float _Samples;
            float4 _Aspect;
            float4 _Scale;
            float2 _NoiseOffset;

            float _Falloff;
            float _Intensity;

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

#ifdef RAY_BOUNCES
                const float4 col = tex2D(_ColorTex, uvPos).rgba;
                if (col.a == 1)
                {
                    float3 result = col.rgb;
#ifdef FALLOFF_IMPACT
                    result *= (1 + distance(uv, uvPos)) / _Falloff;
#endif
                    return result;
                }
                
                uvPos += dir * tex2D(_DistTex, uvPos).rr;
                if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                    return AMBIENT;
                
                [unroll]
                for (int n = 1; n < STEPS; n++)
                {
                    const float4 col = tex2D(_ColorTex, uvPos).rgba;
                    if (col.a == 1)
                    {
                        float3 result = col.rgb + tex2D(_BounceTex, uvPos).rgb;
#ifdef FALLOFF_IMPACT
                        result *= (1 + distance(uv, uvPos)) / _Falloff;
#endif
                        return result;
                    }

                    uvPos += dir * tex2D(_DistTex, uvPos).rr;
                    if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                        return AMBIENT;
                }
#else
                
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

                    uvPos += dir * tex2D(_DistTex, uvPos).rr;
                    if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                        return AMBIENT;
                }
#endif
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
                    result += trace(i.uv, float2(cos(t), sin(t)) / _Aspect.xy);
                }

#ifdef INTENSITY_IMPACT
                result *= _Intensity;
#endif
                result /= _Samples;

                return float4(result, 1);
            }
            ENDHLSL
        }
        
        Pass    // 1
        {
            name "GiBounce"

            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma multi_compile_local FALLOFF_IMPACT _
            #pragma multi_compile_local INTENSITY_IMPACT _
            #pragma multi_compile_local FRAGMENT_RANDOM TEXTURE_RANDOM _
            
            #pragma vertex vert
            #pragma fragment frag

            Texture2D _ColorTex;
            sampler2D _AlphaTex;
            sampler2D _DistTex;
            sampler2D _NoiseTex;
            
            SamplerState linear_clamp_sampler;
            
            float  _Samples;
            float4 _Aspect;
            float2 _NoiseOffset;

            float _Falloff;
            float _Intensity;
            float _IntensityBounce;

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
                float2 uvPos = uv + dir * _Aspect.zw;
                if (tex2D(_AlphaTex, uvPos).r == 1)
                    return AMBIENT;

                [unroll]
                for (int n = 1; n < STEPS; n++)
                {
                    uvPos += dir * tex2D(_DistTex, uvPos).rr;
                    if (uvPos.x < 0 || uvPos.y < 0 || uvPos.x > 1 || uvPos.y > 1)
                        return AMBIENT;
                    
                    if (tex2D(_AlphaTex, uvPos).r == 1)
                    {
                        float3 result = _ColorTex.Sample(linear_clamp_sampler, uvPos).rgb;

#ifdef FALLOFF_IMPACT
                        result *= (1 + distance(uv, uvPos)) / _Falloff;
#endif

                        return result;
                    }
                }

                return AMBIENT;
            }
            
            float4 frag(fragIn_gi i) : SV_Target
            {
                //if (isOutlinePixel(_AlphaTex, i.uv, _Aspect.zw) == false)
                if (tex2D(_AlphaTex, i.uv).r == 0)
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
                    result += trace(i.uv, float2(cos(t), sin(t)) / _Aspect.xy);
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
        
        Pass    // 2
        {
            name "GiOverlay"
            HLSLPROGRAM
            
            #include "Utils.hlsl"
            
            #pragma multi_compile_local INTENSITY_IMPACT _
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _OverlayTex;
            float     _Intensity;

            // =======================================================================            
            float4 frag(fragIn i) : SV_Target
            {
                const float4 overlay = tex2D(_OverlayTex, i.uv);

                if (overlay.a != 1)
                    discard;
                
#ifdef INTENSITY_IMPACT
                return overlay * _Intensity;
#endif
                return overlay; 
            }
            ENDHLSL
        }
        
        Pass    // 3
        {
            name "GiBlitContent"
            HLSLPROGRAM
            
            #include "Utils.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4    _Scale;

            // =======================================================================
            fragIn vert(vertIn v)
            {
                fragIn o;
                o.vertex = v.vertex * _Scale;
                o.uv = v.uv;

                return o;
            }
            
            float4 frag(fragIn i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}