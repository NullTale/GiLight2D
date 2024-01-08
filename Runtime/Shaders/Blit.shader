Shader "Hidden/GiLight2D/Blit"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass    // 0
        {
            name "Blit"
            
            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _MainTex;

            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
        
        Pass    // 1
        {
            name "Alpha Only"
            
            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _MainTex;

            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
                return tex2D(_MainTex, i.uv).a;
            }
            ENDHLSL
        }
        
        Pass    // 2
        {
            name "Merge"
            HLSLPROGRAM
            
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _ATex;
            sampler2D _BTex;

            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
                return tex2D(_ATex, i.uv) + tex2D(_BTex, i.uv);
            }
            ENDHLSL
        }
        
        Pass    // 3
        {
            name "UV"

            HLSLPROGRAM
            #include "Utils.hlsl"
            
            #pragma vertex vert_default
            #pragma fragment frag

            sampler2D _MainTex;

            // =======================================================================
            float4 frag(fragIn i) : SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).a;
                return float4(i.uv, 0, alpha) * (1. - step(alpha, 0));
            }
            ENDHLSL
        }
    }
}