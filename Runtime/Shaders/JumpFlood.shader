Shader "Hidden/GiLight2D/JumpFlood"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            name "JumpFlood"

            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _MainTex;
            float2    _StepSize;

            // =======================================================================
            float2 frag(const fragIn i) : SV_Target
            {
                float min_dist = 1;
                float2 min_dist_uv = float2(0, 0);

                [unroll]
                for (int y = -1; y <= 1; y ++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x ++)
                    {
                        const float2 peek = tex2D(_MainTex, i.uv - float2(x, y) * _StepSize.xy).xy;
                        if (peek.x != 0. && peek.y != 0.)
                        {
                            const float dist = distance(peek, i.uv);
                            if (dist < min_dist)
                            {
                                min_dist = dist;
                                min_dist_uv = peek;
                            }
                        }
                    }
                }

                return min_dist_uv;
            }
            ENDHLSL
        }
    }
}