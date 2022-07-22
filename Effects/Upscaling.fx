sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
float3 uColor;
float3 uSecondaryColor;
float uOpacity : register(C0);
float uSaturation;
float uRotation;
float uTime;
float4 uSourceRect;
float2 uWorldPosition;
float uDirection;
float3 uLightSource;
float2 uImageSize0;
float2 uImageSize1;
float2 uImageSize2;
float4 uShaderSpecificData;

float3 Cubic(float interpolation, float3 value0, float3 value1, float3 value2, float3 value3)
{
    float3 valueDiff = value2 - value1;
    float3 slope1 = (value2 - value0) / 2;
    float3 slope2 = (value3 - value1) / 2;

    float3 a = slope1 + slope2 - 2 * valueDiff;
    float3 b = 3 * valueDiff - 2 * slope1 - slope2;

    return value1 + interpolation * (slope1 + interpolation * (b + interpolation * a));
}


float4 CubicSpline(float2 coords : TEXCOORD0) : COLOR0
{
    coords.x -= uShaderSpecificData.x;
    float interpolation = (uShaderSpecificData.y * coords.x) % 1;

    return float4(Cubic(
        interpolation,
        tex2D(uImage0, coords + float2(-uShaderSpecificData.z, 0)).rgb,
        tex2D(uImage0, coords).rgb,
        tex2D(uImage0, coords + float2(uShaderSpecificData.z, 0)).rgb,
        tex2D(uImage0, coords + float2(2 * uShaderSpecificData.z, 0)).rgb
    ), 1);
}

float4 NoFilter(float2 coords : TEXCOORD0) : COLOR0
{
    return tex2D(uImage0, coords);
}

technique Technique1
{
    pass UpscaleSmooth
    {
        PixelShader = compile ps_2_0 CubicSpline();
    }

    pass UpscaleNoFilter
    {
        PixelShader = compile ps_2_0 NoFilter();
    }
}