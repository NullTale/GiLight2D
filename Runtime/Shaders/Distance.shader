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
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _MainTex;

            float _Offset;

            // =======================================================================
            float frag(const fragIn i) : SV_Target
            {
                float4 sample = tex2D(_MainTex, i.uv);
                return distance(i.uv, sample.xy) + _Offset;
            }
            ENDHLSL
        }
    }
}