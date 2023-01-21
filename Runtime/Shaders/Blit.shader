Shader "Hidden/GiLight2D/Blit"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            name "Blit"
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
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }

    }
}