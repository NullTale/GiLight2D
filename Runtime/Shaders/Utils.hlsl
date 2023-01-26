#ifndef GI_LIGHT_2D_INCLUDED
#define GI_LIGHT_2D_INCLUDED


#define AMBIENT		float3(0, 0, 0)
#define STEPS		16

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

float falloff(const in float2 uv_a, const in float2 uv_b, const in float factor)
{
    return (1 + distance(uv_a, uv_b)) / factor;
}

bool notUVSpace(const in float2 uv)
{
    const float2 uvAbs = abs(uv - float2(.5, .5));
    return uvAbs.x > .5 || uvAbs.y > .5;
    
    // return  (uv.x < 0 || uv.y < 0 || uv.x > 1 || uv.y > 1);
}

bool isOutlinePixel(in const sampler2D tex, in const float2 uv, in const float2 texel)
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
