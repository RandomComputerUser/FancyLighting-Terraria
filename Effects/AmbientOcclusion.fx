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

float4 AlphaToGrayscale(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - tex2D(uImage0, coords).a;
    return float4(brightness, brightness, brightness, 1);
}

float4 AlphaToGrayscaleLighter(float2 coords : TEXCOORD0) : COLOR0
{
    float brightness = 1 - 0.5 * tex2D(uImage0, coords).a;
    return float4(brightness, brightness, brightness, 1);
}

float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float2 pix = float2(uShaderSpecificData.x, uShaderSpecificData.y);

    float brightness = (
          (tex2D(uImage0, coords - 8 * pix).r + tex2D(uImage0, coords + 8 * pix).r)
        + 16 * (tex2D(uImage0, coords - 7 * pix).r + tex2D(uImage0, coords + 7 * pix).r)
        + 120 * (tex2D(uImage0, coords - 6 * pix).r + tex2D(uImage0, coords + 6 * pix).r)
        + 560 * (tex2D(uImage0, coords - 5 * pix).r + tex2D(uImage0, coords + 5 * pix).r)
        + 1820 * (tex2D(uImage0, coords - 4 * pix).r + tex2D(uImage0, coords + 4 * pix).r)
        + 4368 * (tex2D(uImage0, coords - 3 * pix).r + tex2D(uImage0, coords + 3 * pix).r)
        + 8008 * (tex2D(uImage0, coords - 2 * pix).r + tex2D(uImage0, coords + 2 * pix).r)
        + 11440 * (tex2D(uImage0, coords - 1 * pix).r + tex2D(uImage0, coords + 1 * pix).r)
        + 12870 * tex2D(uImage0, coords).r
    ) / 65536.0;

    return float4(brightness, brightness, brightness, 1);
}

float4 BlurFinal(float2 coords : TEXCOORD0) : COLOR0
{
    float2 pix = float2(uShaderSpecificData.x, uShaderSpecificData.y);

    float brightness = (
          (tex2D(uImage0, coords - 8 * pix).r + tex2D(uImage0, coords + 8 * pix).r)
        + 16 * (tex2D(uImage0, coords - 7 * pix).r + tex2D(uImage0, coords + 7 * pix).r)
        + 120 * (tex2D(uImage0, coords - 6 * pix).r + tex2D(uImage0, coords + 6 * pix).r)
        + 560 * (tex2D(uImage0, coords - 5 * pix).r + tex2D(uImage0, coords + 5 * pix).r)
        + 1820 * (tex2D(uImage0, coords - 4 * pix).r + tex2D(uImage0, coords + 4 * pix).r)
        + 4368 * (tex2D(uImage0, coords - 3 * pix).r + tex2D(uImage0, coords + 3 * pix).r)
        + 8008 * (tex2D(uImage0, coords - 2 * pix).r + tex2D(uImage0, coords + 2 * pix).r)
        + 11440 * (tex2D(uImage0, coords - 1 * pix).r + tex2D(uImage0, coords + 1 * pix).r)
        + 12870 * tex2D(uImage0, coords).r
    ) / 65536.0;

    brightness *= brightness * brightness;
    brightness = uShaderSpecificData.z + (1 - uShaderSpecificData.z) * brightness;

    return float4(brightness, brightness, brightness, 1);
}

technique Technique1
{
    pass AlphaToGrayscale
    {
        PixelShader = compile ps_2_0 AlphaToGrayscale();
    }

    pass AlphaToGrayscaleLighter
    {
        PixelShader = compile ps_2_0 AlphaToGrayscaleLighter();
    }

    pass Blur
    {
        PixelShader = compile ps_2_0 Blur();
    }

    pass BlurFinal
    {
        PixelShader = compile ps_2_0 BlurFinal();
    }
}