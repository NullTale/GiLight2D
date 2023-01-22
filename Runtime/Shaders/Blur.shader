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
            #pragma vertex vert
            #pragma fragment frag
            
			#define	BLUR_LENGTH 9
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
			uniform float4 _MainTex_TexelSize;

            // =======================================================================
            struct vertIn
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct fragIn
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // =======================================================================
            fragIn vert(vertIn v)
            {
                fragIn o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            float4 frag(fragIn i) : SV_Target
            {            	
				float4 result = 0;
				float2 uv = i.uv;
            	
				const float2 step = _MainTex_TexelSize;
				uv -= ((BLUR_LENGTH - 1) / 2) * step;
            	
            	[unroll]
				for (int n = 0; n < BLUR_LENGTH; n ++)
				{
					result += tex2D(_MainTex, uv) * k_BlurWeights[n];
					uv += step;
				}
            	return result;
            }
            ENDHLSL
        }
    }
}