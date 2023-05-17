sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s1);
sampler DitherSampler : register(s2);
sampler AmbientOcclusionSampler : register(s3);

float2 NormalMapResolution;
float2 NormalMapRadius;
float HiDefNormalMapStrength;
float HiDefNormalMapExp;
float2 WorldCoordMult;
float2 DitherCoordMult;
float2 AmbientOcclusionCoordMult;

// Gamma correction only applies when both overbright and HiDef are enabled
// The light map uses linear RGB

#define WORLD_TEX_COORDS (WorldCoordMult * coords)
#define MIN_PREMULTIPLIER (0.5 / 255)

// Macro with gamma correction
#define SurfaceColorWithLighting(lightLevel) \
    (LinearToSrgb(float4((lightLevel), 1) * SrgbToLinear(tex2D(WorldSampler, WORLD_TEX_COORDS))) + float4(Dither(coords), 0))

float3 GammaToLinear(float3 color)
{
    return color * color;
}

float4 GammaToLinear(float4 color)
{
    color.a = max(color.a, MIN_PREMULTIPLIER);
    return float4(GammaToLinear(color.rgb / color.a), color.a);
}

float3 SrgbToLinear(float3 color)
{
    float3 lowPart = color / 12.92;
    float3 highPart = pow((color + 0.055) / 1.055, 2.4);
    float3 selector = step(color, 0.04045);
    return lerp(highPart, lowPart, selector);
}

float4 SrgbToLinear(float4 color)
{
    color.a = max(color.a, MIN_PREMULTIPLIER);
    return float4(SrgbToLinear(color.rgb / color.a), color.a);
}

float3 LinearToSrgb(float3 color)
{
    float3 lowPart = 12.92 * color;
    float3 highPart = 1.055 * pow(color, 1 / 2.4) - 0.055;
    float3 selector = step(color, 0.0031308);
    return lerp(highPart, lowPart, selector);
}

float4 LinearToSrgb(float4 color)
{
    return float4(LinearToSrgb(color.rgb) * color.a, color.a);
}

float3 OverbrightLightAt(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return (255.0 / 128) * color;
}

float3 OverbrightLightAtHiDef(float2 coords)
{
    float3 color = tex2D(LightSampler, coords).rgb;
    return SrgbToLinear((65535.0 / 16384) * color);
}

float3 Dither(float2 coords)
{
    return (tex2D(DitherSampler, coords * DitherCoordMult).rgb - 128 / 255.0) * (0.5 / 128);
}

float3 AmbientOcclusion(float2 coords)
{
    return tex2D(AmbientOcclusionSampler, coords * AmbientOcclusionCoordMult).rgb;
}

float2 Gradient(
    float3 horizontalColorDiff,
    float3 verticalColorDiff,
    float leftAlpha,
    float rightAlpha,
    float upAlpha,
    float downAlpha
)
{
    float horizontal = dot(horizontalColorDiff, 1);
    float vertical = dot(verticalColorDiff, 1);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (leftAlpha * rightAlpha >= 1) / (2.0 * 3),
        (upAlpha * downAlpha >= 1) / (2.0 * 3)
    );

    return gradient;
}

// Intentionally use sRGB values for simulating normal maps

float2 QualityNormalsGradientBase(float2 coords, float2 worldTexCoords)
{
    float4 left = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0));
    float4 right = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0));
    float4 up = tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y));
    float4 down = tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y));
    float3 upLeft = tex2D(WorldSampler, worldTexCoords - NormalMapResolution).rgb;
    float3 downRight = tex2D(WorldSampler, worldTexCoords + NormalMapResolution).rgb;
    float3 upRight = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb;
    float3 downLeft = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb;

    float3 horizontalColorDiff = (left.rgb - right.rgb) + ((upLeft - downRight) + (downLeft - upRight)) / 2;
    float3 verticalColorDiff = (up.rgb - down.rgb) + ((upLeft - downRight) - (downLeft - upRight)) / 2;

    return Gradient(horizontalColorDiff, verticalColorDiff, left.a, right.a, up.a, down.a);
}

float2 QualityNormalsGradient(float2 coords, float2 worldTexCoords)
{
    float2 gradient = QualityNormalsGradientBase(coords, worldTexCoords);

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 1.0 / 4);

    gradient = sign(gradient) * min(0.4, multiplier * sqrt(abs(gradient)));

    return gradient * NormalMapRadius;
}

float3 QualityNormalsColorHiDef(float2 coords, float2 worldTexCoords)
{
    float2 gradient = QualityNormalsGradientBase(coords, worldTexCoords);
    gradient *= NormalMapRadius;

    float3 originalColor = tex2D(LightSampler, coords);
    float3 colorDiff = tex2D(LightSampler, coords + gradient).rgb - originalColor;

    colorDiff = sign(colorDiff) * min(0.4, HiDefNormalMapStrength * pow(abs(colorDiff), HiDefNormalMapExp));

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 0.29);
    colorDiff *= multiplier * sqrt(originalColor);

    return originalColor + colorDiff;
}

float3 QualityNormalsColorOverbrightHiDef(float2 coords, float2 worldTexCoords)
{
    float2 gradient = QualityNormalsGradientBase(coords, worldTexCoords);
    gradient *= NormalMapRadius;

    float3 originalColor = OverbrightLightAtHiDef(coords);
    float3 colorDiff = OverbrightLightAtHiDef(coords + gradient) - originalColor;

    colorDiff = sign(colorDiff) * min(0.4, HiDefNormalMapStrength * pow(abs(colorDiff), HiDefNormalMapExp));

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 0.29);
    colorDiff *= multiplier * sqrt(originalColor);

    return originalColor + colorDiff;
}

float2 NormalsGradient(float2 coords, float2 worldTexCoords)
{
    float4 left = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0));
    float4 right = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0));
    float4 up = tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y));
    float4 down = tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y));

    float3 horizontalColorDiff = left.rgb - right.rgb;
    float3 verticalColorDiff = up.rgb - down.rgb;

    float2 gradient = Gradient(horizontalColorDiff, verticalColorDiff, left.a, right.a, up.a, down.a);

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 1.0 / 4);

    return sign(gradient) * min(0.4, multiplier * sqrt(abs(gradient))) * NormalMapRadius;
}

float4 QualityNormals(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = QualityNormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(tex2D(LightSampler, coords + gradient).rgb, 1);
}

float4 QualityNormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = QualityNormalsColorHiDef(coords, WORLD_TEX_COORDS);

    return float4(color, 1) + float4(Dither(coords), 0);
}

float4 QualityNormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = QualityNormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 QualityNormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = QualityNormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return SurfaceColorWithLighting(color);
}

float4 QualityNormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = QualityNormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS)
        * float4(AmbientOcclusion(coords), 1);
}

float4 QualityNormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = QualityNormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return SurfaceColorWithLighting(color * AmbientOcclusion(coords));
}

float4 QualityNormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = QualityNormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return float4(LinearToSrgb(color) + Dither(coords), 1);
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(tex2D(LightSampler, coords + gradient).rgb, 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return SurfaceColorWithLighting(OverbrightLightAtHiDef(coords + gradient));
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(LinearToSrgb(OverbrightLightAtHiDef(coords + gradient)) + Dither(coords), 1);
}

float4 Overbright(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1) * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 OverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return SurfaceColorWithLighting(OverbrightLightAtHiDef(coords));
}

float4 OverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS)
        * float4(AmbientOcclusion(coords), 1);
}

float4 OverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return SurfaceColorWithLighting(
        OverbrightLightAtHiDef(coords) * AmbientOcclusion(coords)
    );
}

float4 OverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(LinearToSrgb(OverbrightLightAtHiDef(coords)) + Dither(coords), 1);
}

float4 OverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(max(OverbrightLightAt(coords), 1), 1) * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 OverbrightMaxHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return SurfaceColorWithLighting(max(OverbrightLightAtHiDef(coords), 1));
}

float4 GammaCorrection(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return LinearToSrgb(
        SrgbToLinear(color)
        * SrgbToLinear(tex2D(TextureSampler, coords))
    );
}

float4 GammaCorrectionBG(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // Multiply by 1.125 to partly compensate for global brightness of 1.2
    color.rgb *= 1.125;

    return LinearToSrgb(
        GammaToLinear(color)
        * SrgbToLinear(tex2D(TextureSampler, coords))
    );
}

technique Technique1
{
    pass QualityNormals
    {
        PixelShader = compile ps_2_0 QualityNormals();
    }

    pass QualityNormalsHiDef
    {
        PixelShader = compile ps_3_0 QualityNormalsHiDef();
    }

    pass QualityNormalsOverbright
    {
        PixelShader = compile ps_2_0 QualityNormalsOverbright();
    }

    pass QualityNormalsOverbrightHiDef
    {
        PixelShader = compile ps_3_0 QualityNormalsOverbrightHiDef();
    }

    pass QualityNormalsOverbrightAmbientOcclusion
    {
        PixelShader = compile ps_2_0 QualityNormalsOverbrightAmbientOcclusion();
    }

    pass QualityNormalsOverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 QualityNormalsOverbrightAmbientOcclusionHiDef();
    }

    pass QualityNormalsOverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 QualityNormalsOverbrightLightOnlyHiDef();
    }

    pass Normals
    {
        PixelShader = compile ps_2_0 Normals();
    }

    pass NormalsOverbright
    {
        PixelShader = compile ps_2_0 NormalsOverbright();
    }

    pass NormalsOverbrightHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightHiDef();
    }

    pass NormalsOverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyHiDef();
    }

    pass Overbright
    {
        PixelShader = compile ps_2_0 Overbright();
    }

    pass OverbrightHiDef
    {
        PixelShader = compile ps_3_0 OverbrightHiDef();
    }

    pass OverbrightAmbientOcclusion
    {
        PixelShader = compile ps_2_0 OverbrightAmbientOcclusion();
    }

    pass OverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightAmbientOcclusionHiDef();
    }

    pass OverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyHiDef();
    }

    pass OverbrightMax
    {
        PixelShader = compile ps_2_0 OverbrightMax();
    }

    pass OverbrightMaxHiDef
    {
        PixelShader = compile ps_3_0 OverbrightMaxHiDef();
    }

    pass GammaCorrection
    {
        PixelShader = compile ps_3_0 GammaCorrection();
    }

    pass GammaCorrectionBG
    {
        PixelShader = compile ps_3_0 GammaCorrectionBG();
    }
}
