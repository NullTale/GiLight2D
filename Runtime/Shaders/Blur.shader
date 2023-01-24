Shader "Hidden/GiLight2D/Blur"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            name "Blur"
        	
            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag
            
            #pragma multi_compile_local HORIZONTAL VERTICAL CROSS BOX
            
			#define	BLUR_LENGTH 9
			#define	BLUR_LENGTH_HALF ((BLUR_LENGTH - 1) / 2)
			static const float	k_BlurWeights[BLUR_LENGTH] =
			{
				0.026995,
				0.064759,
				0.120985,
				0.176033,
				0.199471,
				0.176033,
				0.120985,
				0.064759,
				0.026995,
			};

            sampler2D _MainTex;
			float2    _Step;

            // =======================================================================
            float4 _sample(float2 uv, in const float2 step)
            {
				float4 result = 0;
				uv -= BLUR_LENGTH_HALF * step;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
					result += tex2D(_MainTex, uv) * k_BlurWeights[n];
					uv += step;
				}
            	
            	return result;
            }
            
            float4 frag(fragIn i) : SV_Target
            {            	
				float4 result = 0;

            	//HORIZONTAL VERTICAL CROSS BOX
#ifdef HORIZONTAL
				result = _sample(i.uv, float2(_Step.x, 0));
#endif
#ifdef VERTICAL
				result = _sample(i.uv, float2(0, _Step.y));
#endif
#ifdef CROSS
				result = (_sample(i.uv, _Step) + _sample(i.uv, float2(_Step.x, -_Step.y))) * .5f;
#endif
#ifdef BOX
				const float2 stepX = float2(_Step.x, 0);
				const float2 stepY = float2(0, _Step.y);
				float2 uv = i.uv - BLUR_LENGTH_HALF * stepX;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
					result += _sample(uv, stepY) * k_BlurWeights[n];
					uv += stepX;
				}
#endif            	
            	
            	return result;
            }
            ENDHLSL
        }
    }
}