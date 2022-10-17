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

float4 GenerateNormalMap(float2 coords : TEXCOORD0) : COLOR0
{
    float2 otherCoords = uColor.xy * coords;
    float4 leftColor = tex2D(uImage1, otherCoords - float2(uShaderSpecificData.x, 0));
    float4 rightColor = tex2D(uImage1, otherCoords + float2(uShaderSpecificData.x, 0));
    float4 upColor = tex2D(uImage1, otherCoords - float2(0, uShaderSpecificData.y));
    float4 downColor = tex2D(uImage1, otherCoords + float2(0, uShaderSpecificData.y));

    float horizontal = (leftColor.r + leftColor.g + leftColor.b) - (rightColor.r + rightColor.g + rightColor.b);
    float vertical = (upColor.r + upColor.g + upColor.b) - (downColor.r + downColor.g + downColor.b);

    horizontal *= leftColor.a * rightColor.a < 1 ? 0 : 1.0 / 3.0;
    vertical *= upColor.a * downColor.a < 1 ? 0 : 1.0 / 3.0;

    float3 color = tex2D(uImage1, otherCoords).rgb;
    float multiplier = 1 - 0.75 * (color.r + color.g + color.b) / 3.0;

    horizontal = sign(horizontal) * min(multiplier * sqrt(abs(horizontal)), 0.4);
    vertical = sign(vertical) * min(multiplier * sqrt(abs(vertical)), 0.4);

    return float4(tex2D(uImage0, coords + float2(horizontal, vertical) * uShaderSpecificData.zw).rgb, 1);
}

float4 NormalMapOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 otherCoords = uColor.xy * coords;
    float4 leftColor = tex2D(uImage1, otherCoords - float2(uShaderSpecificData.x, 0));
    float4 rightColor = tex2D(uImage1, otherCoords + float2(uShaderSpecificData.x, 0));
    float4 upColor = tex2D(uImage1, otherCoords - float2(0, uShaderSpecificData.y));
    float4 downColor = tex2D(uImage1, otherCoords + float2(0, uShaderSpecificData.y));

    float horizontal = (leftColor.r + leftColor.g + leftColor.b) - (rightColor.r + rightColor.g + rightColor.b);
    float vertical = (upColor.r + upColor.g + upColor.b) - (downColor.r + downColor.g + downColor.b);

    horizontal *= leftColor.a * rightColor.a < 1 ? 0 : 1.0 / 3.0;
    vertical *= upColor.a * downColor.a < 1 ? 0 : 1.0 / 3.0;

    float3 color = tex2D(uImage1, otherCoords).rgb;
    float multiplier = 1 - 0.75 * (color.r + color.g + color.b) / 3.0;

    horizontal = sign(horizontal) * min(multiplier * sqrt(abs(horizontal)), 0.4);
    vertical = sign(vertical) * min(multiplier * sqrt(abs(vertical)), 0.4);

    return float4(255.0 / 128.0 * tex2D(uImage0, coords + float2(horizontal, vertical) * uShaderSpecificData.zw).rgb, 1)
           * tex2D(uImage1, otherCoords);
}

float4 DirectOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 otherCoords = uColor.xy * coords;
    return float4(255.0 / 128.0 * tex2D(uImage0, coords).rgb, 1) * tex2D(uImage1, otherCoords);
}

float4 DirectOverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    float2 otherCoords = uColor.xy * coords;
    return float4(max(255.0 / 128.0 * tex2D(uImage0, coords).rgb, float3(1, 1, 1)), 1) * tex2D(uImage1, otherCoords);
}

technique Technique1
{
    pass SimulateNormals
    {
        PixelShader = compile ps_2_0 GenerateNormalMap();
    }

    pass SimulateNormalsOverbright
    {
        PixelShader = compile ps_2_0 NormalMapOverbright();
    }

    pass Overbright
    {
        PixelShader = compile ps_2_0 DirectOverbright();
    }

    pass OverbrightMax
    {
        PixelShader = compile ps_2_0 DirectOverbrightMax();
    }
}