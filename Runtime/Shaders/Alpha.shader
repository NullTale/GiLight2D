Shader "Hidden/GiLight2D/Alpha"
{
	SubShader
	{
		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			name "UV"
			
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _MainTex;
			sampler2D _AlphaTex;

			float _UvScale;
			
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
				return float4(tex2D(_MainTex, i.uv).rgb, 1. - tex2D(_AlphaTex, (i.uv - .5f) * _UvScale + .5f).a);
			}
			ENDHLSL
		}
	}
}
