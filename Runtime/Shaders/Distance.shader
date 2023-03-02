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

            float  _Offset;
            float2 _Aspect;

            // =======================================================================
            float frag(fragIn i) : SV_Target
            {
                float2 sample = tex2D(_MainTex, i.uv);
                i.uv.x   *= _Aspect;
                sample.x *= _Aspect;
                return distance(i.uv, sample.xy) + _Offset;
            }
            ENDHLSL
        }
    }
}