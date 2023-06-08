sampler OccluderSampler : register(s0);

float2 BlurSize;
float BlurPower;

float SemicircleWeight[7] = { 0.090184614, 0.0883153, 0.08445264, 0.078302175, 0.069257066, 0.055930126, 0.033558074 };
float LargeSemicircleWeight[11] = {
    0.057642538, 0.057163175, 0.056192182, 0.05470338, 0.052652873,
    0.049971554, 0.04655055, 0.042210396, 0.036625776, 0.029088551,
    0.017199023
};

float GaussianWeight[7] = { 0.22558594, 0.19335938, 0.12084961, 0.053710938, 0.016113281, 0.0029296875, 0.00024414062 };

float SemicircleBlurBrightness(float2 coords)
{
    float brightness = 0.0;

    float t = 0.5;
    for (int i = 0; i < 7; ++i)
    {
        float2 offset = t * BlurSize;
        brightness += SemicircleWeight[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );

        t += 1.0;
    }

    return brightness;
}

float LargeSemicircleBlurBrightness(float2 coords)
{
    float brightness = 0.0;

    float t = 0.5;
    for (int i = 0; i < 11; ++i)
    {
        float2 offset = t * BlurSize;
        brightness += LargeSemicircleWeight[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );

        t += 1.0;
    }

    return brightness;
}

float BlurBrightness(float2 coords)
{
    float brightness = GaussianWeight[0] * tex2D(OccluderSampler, coords).r;

    for (int i = 1; i <= 6; ++i)
    {
        float2 offset = i * BlurSize;
        brightness += GaussianWeight[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );
    }

    return brightness;
}

float4 AlphaToMonochrome(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - tex2D(OccluderSampler, coords).a;
    return float4(brightness.x, 0, 0, 0);
}

float4 AlphaToLightMonochrome(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = -0.625 * tex2D(OccluderSampler, coords).a + 1;
    return float4(brightness.x, 0, 0, 0);
}

float4 SemicircleBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = SemicircleBlurBrightness(coords);

    return float4(brightness.x, 0, 0, 0);
}

float4 LargeSemicircleBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = LargeSemicircleBlurBrightness(coords);

    return float4(brightness.x, 0, 0, 0);
}

float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BlurBrightness(coords);

    return float4(brightness.x, 0, 0, 0);
}

float4 FinalBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BlurBrightness(coords);
    brightness = pow(brightness, BlurPower);

    return float4(brightness.xxx, 1);
}

technique Technique1
{
    pass AlphaToMonochrome
    {
        PixelShader = compile ps_2_0 AlphaToMonochrome();
    }

    pass AlphaToLightMonochrome
    {
        PixelShader = compile ps_2_0 AlphaToLightMonochrome();
    }

    pass SemicircleBlur
    {
        PixelShader = compile ps_2_0 SemicircleBlur();
    }

    pass LargeSemicircleBlur
    {
        PixelShader = compile ps_2_0 LargeSemicircleBlur();
    }

    pass Blur
    {
        PixelShader = compile ps_2_0 Blur();
    }

    pass FinalBlur
    {
        PixelShader = compile ps_2_0 FinalBlur();
    }
}
