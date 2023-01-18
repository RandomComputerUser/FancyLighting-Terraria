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

float4 QualityNormalMap(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;

    float4 left = tex2D(uImage1, worldTexCoords - float2(uShaderSpecificData.x, 0));
    float4 right = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, 0));
    float4 up = tex2D(uImage1, worldTexCoords - float2(0, uShaderSpecificData.y));
    float4 down = tex2D(uImage1, worldTexCoords + float2(0, uShaderSpecificData.y));
    float3 upLeft = tex2D(uImage1, worldTexCoords - uShaderSpecificData.xy).rgb;
    float3 downRight = tex2D(uImage1, worldTexCoords + uShaderSpecificData.xy).rgb;
    float3 upRight = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, -uShaderSpecificData.y)).rgb;
    float3 downLeft = tex2D(uImage1, worldTexCoords + float2(-uShaderSpecificData.x, uShaderSpecificData.y)).rgb;

    float3 horizontalColorDiff = (left.rgb - right.rgb) + ((upLeft - downRight) + (downLeft - upRight)) / 2;
    float3 verticalColorDiff = (up.rgb - down.rgb) + ((upLeft - downRight) - (downLeft - upRight)) / 2;

    float horizontal = dot(horizontalColorDiff, (1).xxx);
    float vertical = dot(verticalColorDiff, (1).xxx);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (left.a * right.a >= 1) / (2.0 * 3),
        (up.a * down.a >= 1) / (2.0 * 3)
    );

    float3 color = tex2D(uImage1, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, (1.0 / 4).xxx);

    gradient = sign(gradient) * min(multiplier * sqrt(abs(gradient)), (0.4).xx);

    return float4(tex2D(uImage0, coords + gradient * uShaderSpecificData.zw).rgb, 1);
}

float4 QualityNormalMapOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;

    float4 left = tex2D(uImage1, worldTexCoords - float2(uShaderSpecificData.x, 0));
    float4 right = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, 0));
    float4 up = tex2D(uImage1, worldTexCoords - float2(0, uShaderSpecificData.y));
    float4 down = tex2D(uImage1, worldTexCoords + float2(0, uShaderSpecificData.y));
    float3 upLeft = tex2D(uImage1, worldTexCoords - uShaderSpecificData.xy).rgb;
    float3 downRight = tex2D(uImage1, worldTexCoords + uShaderSpecificData.xy).rgb;
    float3 upRight = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, -uShaderSpecificData.y)).rgb;
    float3 downLeft = tex2D(uImage1, worldTexCoords + float2(-uShaderSpecificData.x, uShaderSpecificData.y)).rgb;

    float3 horizontalColorDiff = (left.rgb - right.rgb) + ((upLeft - downRight) + (downLeft - upRight)) / 2;
    float3 verticalColorDiff = (up.rgb - down.rgb) + ((upLeft - downRight) - (downLeft - upRight)) / 2;

    float horizontal = dot(horizontalColorDiff, (1).xxx);
    float vertical = dot(verticalColorDiff, (1).xxx);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (left.a * right.a >= 1) / (2.0 * 3),
        (up.a * down.a >= 1) / (2.0 * 3)
    );

    float3 color = tex2D(uImage1, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, (1.0 / 4).xxx);

    gradient = sign(gradient) * min(multiplier * sqrt(abs(gradient)), (0.4).xx);

    return float4(255.0 / 128 * tex2D(uImage0, coords + gradient * uShaderSpecificData.zw).rgb, 1)
        * tex2D(uImage1, worldTexCoords);
}

float4 GenerateNormalMap(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;
    float4 left = tex2D(uImage1, worldTexCoords - float2(uShaderSpecificData.x, 0));
    float4 right = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, 0));
    float4 up = tex2D(uImage1, worldTexCoords - float2(0, uShaderSpecificData.y));
    float4 down = tex2D(uImage1, worldTexCoords + float2(0, uShaderSpecificData.y));

    float3 horizontalColorDiff = left.rgb - right.rgb;
    float3 verticalColorDiff = up.rgb - down.rgb;

    float horizontal = dot(horizontalColorDiff, (1).xxx);
    float vertical = dot(verticalColorDiff, (1).xxx);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (left.a * right.a >= 1) / (1.0 * 3),
        (up.a * down.a >= 1) / (1.0 * 3)
    );

    float3 color = tex2D(uImage1, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, (1.0 / 4).xxx);

    gradient = sign(gradient) * min(multiplier * sqrt(abs(gradient)), (0.4).xx);

    return float4(tex2D(uImage0, coords + gradient * uShaderSpecificData.zw).rgb, 1);
}

float4 NormalMapOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;
    float4 left = tex2D(uImage1, worldTexCoords - float2(uShaderSpecificData.x, 0));
    float4 right = tex2D(uImage1, worldTexCoords + float2(uShaderSpecificData.x, 0));
    float4 up = tex2D(uImage1, worldTexCoords - float2(0, uShaderSpecificData.y));
    float4 down = tex2D(uImage1, worldTexCoords + float2(0, uShaderSpecificData.y));

    float3 horizontalColorDiff = left.rgb - right.rgb;
    float3 verticalColorDiff = up.rgb - down.rgb;

    float horizontal = dot(horizontalColorDiff, (1).xxx);
    float vertical = dot(verticalColorDiff, (1).xxx);
    float2 gradient = float2(horizontal, vertical);

    gradient *= float2(
        (left.a * right.a >= 1) / (1.0 * 3),
        (up.a * down.a >= 1) / (1.0 * 3)
    );

    float3 color = tex2D(uImage1, worldTexCoords).rgb;
    float multiplier = 1 - dot(color, (1.0 / 4).xxx);

    gradient = sign(gradient) * min(multiplier * sqrt(abs(gradient)), (0.4).xx);

    return float4(255.0 / 128 * tex2D(uImage0, coords + gradient * uShaderSpecificData.zw).rgb, 1)
           * tex2D(uImage1, worldTexCoords);
}

float4 DirectOverbright(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;
    return float4(255.0 / 128 * tex2D(uImage0, coords).rgb, 1) * tex2D(uImage1, worldTexCoords);
}

float4 DirectOverbrightMax(float2 coords : TEXCOORD0) : COLOR0
{
    float2 worldTexCoords = uColor.xy * coords;
    return float4(max(255.0 / 128 * tex2D(uImage0, coords).rgb, (1).xxx), 1) * tex2D(uImage1, worldTexCoords);
}

technique Technique1
{
    pass QualityNormals
    {
        PixelShader = compile ps_2_0 QualityNormalMap();
    }

    pass QualityNormalsOverbright
    {
        PixelShader = compile ps_2_0 QualityNormalMapOverbright();
    }

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