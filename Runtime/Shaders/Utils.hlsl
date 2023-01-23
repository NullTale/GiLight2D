#ifndef GI_LIGHT_2D_INCLUDED
#define GI_LIGHT_2D_INCLUDED

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

fragIn vert_default(vertIn v)
{
    fragIn o;
    o.vertex = v.vertex;
    o.uv = v.uv;

    return o;
}

bool isOutlinePixel(in const sampler2D tex, in const float2 uv, in const float4 texel)
{
    /*if (tex2D(tex, uv).r == 0)
        return false;
    
    return tex2D(tex, uv + float2(texel.x, 0)).r *
        tex2D(tex, uv - float2(texel.x, 0)).r *
        tex2D(tex, uv + float2(0, texel.y)).r *
        tex2D(tex, uv - float2(0, texel.y)).r == 0;*/
    
    return tex2D(tex, uv).r * 
        (tex2D(tex, uv + float2(texel.x, 0)).r +
        tex2D(tex, uv - float2(texel.x, 0)).r +
        tex2D(tex, uv + float2(0, texel.y)).r +
        tex2D(tex, uv - float2(0, texel.y)).r)
    > 0;
}

float random(in const float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
}

#endif
