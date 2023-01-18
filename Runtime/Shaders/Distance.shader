Shader "Hidden/GiLight2D/Distance"
{
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			name "UVDistance"
			
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _MainTex;

			float _Offset;
			
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
			};

			// =======================================================================
			fragIn vert(const vertIn v)
			{
				fragIn o;
				o.vertex = v.vertex;
				o.uv = v.uv;
				return o;
			}
			
			float frag(const fragIn i) : SV_Target
			{
				float4 sample = tex2D(_MainTex, i.uv);
				return distance(i.uv, sample.xy) + _Offset;
			}
			ENDHLSL
		}
	}
}
