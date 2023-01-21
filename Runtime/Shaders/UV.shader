Shader "Hidden/GiLight2D/UV"
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
                float alpha = tex2D(_MainTex, i.uv).a;
                return float4(i.uv, 0, alpha) * (1. - step(alpha, 0));
            }
            ENDHLSL
        }
    }
}