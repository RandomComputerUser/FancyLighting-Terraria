sampler OccluderSampler : register(s0);

float2 BlurSize;
float BrightnessIncrease;

float BlurWeight[9] = { 12870, 11440, 8008, 4368, 1820, 560, 120, 16, 1 };

float BlurBrightness(float2 coords)
{
    float brightness = BlurWeight[0] * tex2D(OccluderSampler, coords);

    for (int i = 1; i <= 8; ++i)
    {
        float2 offset = i * BlurSize;
        brightness += BlurWeight[i] * (
            tex2D(OccluderSampler, coords - offset)
            + tex2D(OccluderSampler, coords + offset)
        );
    }

    return brightness * (1 / 65536.0);
}

float4 AlphaToGrayscale(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - tex2D(OccluderSampler, coords).a;
    return float4(brightness.xxx, 1);
}

float4 AlphaToLighterGrayscale(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = -0.5 * tex2D(OccluderSampler, coords).a + 1;
    return float4(brightness.xxx, 1);
}

float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BlurBrightness(coords);

    return float4(brightness.xxx, 1);
}

float4 FinalBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = BlurBrightness(coords);

    brightness *= brightness * brightness;
    brightness = (1 - BrightnessIncrease) * brightness + BrightnessIncrease;

    return float4(brightness, brightness, brightness, 1);
}

technique Technique1
{
    pass AlphaToGrayscale
    {
        PixelShader = compile ps_2_0 AlphaToGrayscale();
    }

    pass AlphaToLighterGrayscale
    {
        PixelShader = compile ps_2_0 AlphaToLighterGrayscale();
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
