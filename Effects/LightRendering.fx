sampler TextureSampler : register(s0);

sampler LightSampler : register(s0);
sampler WorldSampler : register(s1);
sampler DitherSampler : register(s2);
sampler AmbientOcclusionSampler : register(s3);

float2 NormalMapResolution;
float2 NormalMapRadius;
float HiDefNormalMapStrength;
float2 WorldCoordMult;
float2 DitherCoordMult;
float2 AmbientOcclusionCoordMult;

// Gamma correction only applies when both overbright and HiDef are enabled

// Possible addition: tonemapping
// None of the tonemapping functions I tried looked good

#define WORLD_TEX_COORDS (WorldCoordMult * coords)
#define MIN_PREMULTIPLIER (0.5 / 255)

// Macros with gamma correction
#define SurfaceColor(color) \
    LinearToSrgb(color)
#define SurfaceColorWithLighting(lightLevel) \
    (SurfaceColor(float4((lightLevel), 1) * SrgbToLinear(tex2D(WorldSampler, WORLD_TEX_COORDS))) + float4(Dither(coords), 0))

float3 GammaToLinear(float3 color)
{
    return pow(color, 2.2);
}

float4 GammaToLinear(float4 color)
{
    color.a = max(color.a, MIN_PREMULTIPLIER);
    float mult = 1 / color.a;
    return float4(GammaToLinear(color.rgb * mult), color.a);
}

float3 SrgbToLinear(float3 color)
{
    float3 lowPart = color * (1 / 12.92);
    float3 highPart = pow((color + 0.055) * (1 / 1.055), 2.4);
    float3 selector = step(color, 0.04045);
    return lerp(highPart, lowPart, selector);
}

float4 SrgbToLinear(float4 color)
{
    color.a = max(color.a, MIN_PREMULTIPLIER);
    float mult = 1 / color.a;
    return float4(SrgbToLinear(color.rgb * mult), color.a);
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
    return GammaToLinear((65535.0 / 16384) * color);
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
        (leftAlpha * rightAlpha) * (1 / (2.0 * 3)),
        (upAlpha * downAlpha) * (1 / (2.0 * 3))
    );

    return gradient;
}

// Intentionally use sRGB values for simulating normal maps

float2 NormalsGradientBase(float2 coords, float2 worldTexCoords)
{
    float4 left = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, 0));
    float4 right = tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, 0));
    float4 up = tex2D(WorldSampler, worldTexCoords - float2(0, NormalMapResolution.y));
    float4 down = tex2D(WorldSampler, worldTexCoords + float2(0, NormalMapResolution.y));
    float3 positiveDiagonal
        = tex2D(WorldSampler, worldTexCoords - NormalMapResolution).rgb // up left
        - tex2D(WorldSampler, worldTexCoords + NormalMapResolution).rgb; // down right
    float3 negativeDiagonal
        = tex2D(WorldSampler, worldTexCoords - float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb // down left
        - tex2D(WorldSampler, worldTexCoords + float2(NormalMapResolution.x, -NormalMapResolution.y)).rgb; // up right

    float3 horizontalColorDiff = 0.5 * (positiveDiagonal + negativeDiagonal) + (left.rgb - right.rgb);
    float3 verticalColorDiff = 0.5 * (positiveDiagonal - negativeDiagonal) + (up.rgb - down.rgb);

    return Gradient(horizontalColorDiff, verticalColorDiff, left.a, right.a, up.a, down.a);
}

float2 NormalsGradient(float2 coords, float2 worldTexCoords)
{
    float2 gradient = NormalsGradientBase(coords, worldTexCoords);

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 25.0 - dot(color, 25.0 / 3.2);

    gradient = sign(gradient) * (1.0 - rsqrt(abs(multiplier * gradient) + 1.0));

    return gradient * NormalMapRadius;
}

float3 NormalsColorHiDef(float2 coords, float2 worldTexCoords)
{
    float2 gradient = NormalsGradientBase(coords, worldTexCoords);
    gradient *= NormalMapRadius;

    float3 originalColor = tex2D(LightSampler, coords);
    float3 colorDiff = tex2D(LightSampler, coords + gradient).rgb - originalColor;

    colorDiff = sign(colorDiff) * 0.6 * (1.0 - rsqrt(abs(HiDefNormalMapStrength * colorDiff) + 1.0));

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 1.0 / 3.2);
    colorDiff *= multiplier * sqrt(originalColor);

    return originalColor + colorDiff;
}

float3 NormalsColorOverbrightHiDef(float2 coords, float2 worldTexCoords)
{
    float2 gradient = NormalsGradientBase(coords, worldTexCoords);
    gradient *= NormalMapRadius;

    float3 originalColor = OverbrightLightAtHiDef(coords);
    float3 colorDiff = OverbrightLightAtHiDef(coords + gradient) - originalColor;

    colorDiff = sign(colorDiff) * 0.6 * (1.0 - rsqrt(abs(HiDefNormalMapStrength * colorDiff) + 1.0));

    float3 color = tex2D(WorldSampler, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, 1.0 / 3.2);
    colorDiff *= multiplier * sqrt(originalColor);

    return originalColor + colorDiff;
}

float4 Normals(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(tex2D(LightSampler, coords + gradient).rgb, 1);
}

float4 NormalsHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorHiDef(coords, WORLD_TEX_COORDS);

    return float4(color + Dither(coords), 1);
}

float4 NormalsOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 NormalsOverbrightHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return SurfaceColorWithLighting(color);
}

float4 NormalsOverbrightAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient) * AmbientOcclusion(coords), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 NormalsOverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return SurfaceColorWithLighting(color * AmbientOcclusion(coords));
}

float4 NormalsOverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS).a;
}

float4 NormalsOverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return float4(SurfaceColor(color) + Dither(coords), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS).a;
}

float4 NormalsOverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(OverbrightLightAt(coords + gradient), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return float4(SurfaceColor(color) + Dither(coords), 1);
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    float2 gradient = NormalsGradient(coords, WORLD_TEX_COORDS);

    return float4(
        OverbrightLightAt(coords + gradient)
            * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WORLD_TEX_COORDS).a),
        1
    );
}

float4 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    float3 color = NormalsColorOverbrightHiDef(coords, WORLD_TEX_COORDS);

    return float4(
        SurfaceColor(
                color * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WORLD_TEX_COORDS).a)
            ) + Dither(coords),
        1
    );
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
    return float4(OverbrightLightAt(coords) * AmbientOcclusion(coords), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS);
}

float4 OverbrightAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return SurfaceColorWithLighting(
        OverbrightLightAtHiDef(coords) * AmbientOcclusion(coords)
    );
}

float4 OverbrightLightOnly(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1) * tex2D(WorldSampler, WORLD_TEX_COORDS).a;
}

float4 OverbrightLightOnlyHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(SurfaceColor(OverbrightLightAtHiDef(coords)) + Dither(coords), 1)
        * tex2D(WorldSampler, WORLD_TEX_COORDS).a;
}

float4 OverbrightLightOnlyOpaque(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(OverbrightLightAt(coords), 1);
}

float4 OverbrightLightOnlyOpaqueHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(SurfaceColor(OverbrightLightAtHiDef(coords)) + Dither(coords), 1);
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusion(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        OverbrightLightAt(coords)
            * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WORLD_TEX_COORDS).a),
        1
    );
}

float4 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef(float2 coords : TEXCOORD0) : COLOR0
{
    return float4(
        SurfaceColor(
                OverbrightLightAtHiDef(coords)
                * lerp(1, AmbientOcclusion(coords), tex2D(WorldSampler, WORLD_TEX_COORDS).a)
            ) + Dither(coords),
        1
    );
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
    return SurfaceColor(
        GammaToLinear(color)
        * SrgbToLinear(tex2D(TextureSampler, coords))
    );
}

float4 GammaCorrectionLightOnly(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    return SurfaceColor(GammaToLinear(color)) * tex2D(TextureSampler, coords).a;
}

float4 GammaCorrectionBG(float4 color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // Multiply by 1.1 to partly compensate for global brightness of 1.2
    // Multiplying by 1.2 makes clouds too bright
    color.rgb *= 1.1;

    return SurfaceColor(
        SrgbToLinear(color)
        * SrgbToLinear(tex2D(TextureSampler, coords))
    );
}

technique Technique1
{
    pass Normals
    {
        PixelShader = compile ps_2_0 Normals();
    }

    pass NormalsHiDef
    {
        PixelShader = compile ps_3_0 NormalsHiDef();
    }

    pass NormalsOverbright
    {
        PixelShader = compile ps_2_0 NormalsOverbright();
    }

    pass NormalsOverbrightHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightHiDef();
    }

    pass NormalsOverbrightAmbientOcclusion
    {
        PixelShader = compile ps_2_0 NormalsOverbrightAmbientOcclusion();
    }

    pass NormalsOverbrightAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightAmbientOcclusionHiDef();
    }

    pass NormalsOverbrightLightOnly
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnly();
    }

    pass NormalsOverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnlyOpaque();
    }

    pass NormalsOverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueHiDef();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_2_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 NormalsOverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
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

    pass OverbrightLightOnly
    {
        PixelShader = compile ps_2_0 OverbrightLightOnly();
    }

    pass OverbrightLightOnlyHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyHiDef();
    }

    pass OverbrightLightOnlyOpaque
    {
        PixelShader = compile ps_2_0 OverbrightLightOnlyOpaque();
    }

    pass OverbrightLightOnlyOpaqueHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueHiDef();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusion
    {
        PixelShader = compile ps_2_0 OverbrightLightOnlyOpaqueAmbientOcclusion();
    }

    pass OverbrightLightOnlyOpaqueAmbientOcclusionHiDef
    {
        PixelShader = compile ps_3_0 OverbrightLightOnlyOpaqueAmbientOcclusionHiDef();
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

    pass GammaCorrectionLightOnly
    {
        PixelShader = compile ps_3_0 GammaCorrectionLightOnly();
    }

    pass GammaCorrectionBG
    {
        PixelShader = compile ps_3_0 GammaCorrectionBG();
    }
}
