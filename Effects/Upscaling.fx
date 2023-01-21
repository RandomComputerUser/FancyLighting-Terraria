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

// 0.5503212081491045 == 1 / cbrt(6)

// https://stackoverflow.com/a/42179924
// https://web.archive.org/web/20180927181721/http://www.java-gaming.org/index.php?topic=35123.0
float4 Cubic(float v)
{
    float3 n = float3(1.0 * 0.5503212081491045, 2.0 * 0.5503212081491045, 3.0 * 0.5503212081491045) - v;
    n *= n * n;
    float x = n.x;
    float y = n.y - 4.0 * n.x;
    float z = n.z - 4.0 * n.y + 6.0 * n.x;
    float w = 1.0 - x - y - z;
    return float4(x, y, z, w);
}

float4 Bicubic(float2 coords : TEXCOORD0) : COLOR0
{
    float2 texCoords = uShaderSpecificData.zw * coords - 0.5;

    float2 fxy = frac(texCoords);
    texCoords -= fxy;
    fxy *= 0.5503212081491045;

    float4 xcubic = Cubic(fxy.x);
    float4 ycubic = Cubic(fxy.y);

    float4 c = texCoords.xxyy + float2(-0.5, 1.5).xyxy;

    float4 s = float4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    float4 offset = c + float4(xcubic.yw, ycubic.yw) / s;

    offset *= uShaderSpecificData.xxyy;

    float3 sample0 = tex2D(uImage0, offset.xz).rgb;
    float3 sample1 = tex2D(uImage0, offset.yz).rgb;
    float3 sample2 = tex2D(uImage0, offset.xw).rgb;
    float3 sample3 = tex2D(uImage0, offset.yw).rgb;

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    float3 color = lerp(lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);

    // Dithering
    color += (tex2D(uImage1, coords * uColor.xy).rgb - 0.25) / 128;

    return float4(color, 1);
}

float4 NoFilter(float2 coords : TEXCOORD0) : COLOR0
{
    return tex2D(uImage0, coords);
}

technique Technique1
{
    pass UpscaleBicubic
    {
        PixelShader = compile ps_2_0 Bicubic();
    }

    pass UpscaleNoFilter
    {
        PixelShader = compile ps_2_0 NoFilter();
    }
}