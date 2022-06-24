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
    float4 color = float4(1, 1, 1, 1);
    color.rgb -= tex2D(uImage0, coords).a;
    return color;
}

float4 Blur(float2 coords : TEXCOORD0) : COLOR0
{
    float2 pix = float2(uShaderSpecificData.x, uShaderSpecificData.y);

    float4 color = (
          (tex2D(uImage0, coords - 8 * pix) + tex2D(uImage0, coords + 8 * pix))
        + 16 * (tex2D(uImage0, coords - 7 * pix) + tex2D(uImage0, coords + 7 * pix))
        + 120 * (tex2D(uImage0, coords - 6 * pix) + tex2D(uImage0, coords + 6 * pix))
        + 560 * (tex2D(uImage0, coords - 5 * pix) + tex2D(uImage0, coords + 5 * pix))
        + 1820 * (tex2D(uImage0, coords - 4 * pix) + tex2D(uImage0, coords + 4 * pix))
        + 4368 * (tex2D(uImage0, coords - 3 * pix) + tex2D(uImage0, coords + 3 * pix))
        + 8008 * (tex2D(uImage0, coords - 2 * pix) + tex2D(uImage0, coords + 2 * pix))
        + 11440 * (tex2D(uImage0, coords - 1 * pix) + tex2D(uImage0, coords + 1 * pix))
        + 12870 * tex2D(uImage0, coords)
    ) / 65536.0;

    color.a = 1;

    if (uShaderSpecificData.z > 0)
    {
        color.rgb *= color.rgb * color.rgb;
        color.rgb = uShaderSpecificData.w + (1 - uShaderSpecificData.w) * color.rgb;
    }

    return color;
}

technique Technique1
{
    pass AlphaToGrayscale
    {
        PixelShader = compile ps_2_0 AlphaToGrayscale();
    }

    pass Blur
    {
        PixelShader = compile ps_2_0 Blur();
    }
}