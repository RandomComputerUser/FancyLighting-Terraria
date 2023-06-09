sampler OccluderSampler : register(s0);

float2 BlurSize;
float BlurPower;

float HemisphereKernel[8] =
{
    0.08443596, 0.08368205, 0.08137844, 0.07738684, 0.07142482, 0.06293485, 0.05066158, 0.030313438
};

float GaussianKernel[8] =
{
    0.20947266, 0.18328857, 0.12219238, 0.06109619, 0.022216797, 0.005554199, 0.0008544922, 6.1035156e-05
};

float HemisphereBlurBrightness(float2 coords)
{
    float brightness = HemisphereKernel[0] * tex2D(OccluderSampler, coords).r;

    float2 offset = 0;
    for (int i = 1; i <= 7; ++i)
    {
        offset += BlurSize;
        brightness += HemisphereKernel[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );
    }

    return brightness;
}

float BlurBrightness(float2 coords)
{
    float brightness = GaussianKernel[0] * tex2D(OccluderSampler, coords).r;

    float2 offset = 0;
    for (int i = 1; i <= 7; ++i)
    {
        offset += BlurSize;
        brightness += GaussianKernel[i] * (
            tex2D(OccluderSampler, coords - offset).r
            + tex2D(OccluderSampler, coords + offset).r
        );
    }

    return brightness;
}

float4 AlphaToRed(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - tex2D(OccluderSampler, coords).a;
    return float4(brightness.x, 0, 0, 0);
}

float4 AlphaToLightRed(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = -0.75 * tex2D(OccluderSampler, coords).a + 1;
    return float4(brightness.x, 0, 0, 0);
}

float4 HemisphereBlur(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = HemisphereBlurBrightness(coords);

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
    pass AlphaToRed
    {
        PixelShader = compile ps_2_0 AlphaToRed();
    }

    pass AlphaToLightRed
    {
        PixelShader = compile ps_2_0 AlphaToLightRed();
    }

    pass HemisphereBlur
    {
        PixelShader = compile ps_2_0 HemisphereBlur();
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
