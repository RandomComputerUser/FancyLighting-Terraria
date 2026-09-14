sampler TextureSampler : register(s0);

sampler LuminanceSampler : register(s0);
sampler CloudSampler : register(s8);

float4x4 MatrixTransform;

float2 Scale;
float2 CloudScale;

float Gamma;
float InverseGamma;

float3 BaseColor;

float2 SkyLightGradient;
float SkyLightMult;
float NormalMapStrength;

/* Helper functions *********************************************************************/

float Luminance(float3 color)
{
    return dot(pow(color, Gamma), float3(0.2126, 0.7152, 0.0722));
}

float2x2 CalculateRotationMatrix(float2 texCoord)
{
    float2 partialX = ddx(texCoord);
    float2 partialY = ddy(texCoord);
    
    float2 texelSize = float2(
        length(float2(partialX.x, partialY.x)),
        length(float2(partialX.y, partialY.y))
    );
    float2 textureSize = 1.0 / texelSize;
    
    return float2x2(
        partialX * textureSize,
        partialY * textureSize
    );
}

float NormalsMultiplierFancySky(float2 surfaceGradient, float2 texCoord)
{
    float surfaceGradientLength = length(surfaceGradient);
    
    float2 lightGradient = SkyLightGradient;
    float2x2 rotationMatrix = CalculateRotationMatrix(texCoord);
    lightGradient = mul(lightGradient, rotationMatrix);
    float lightGradientLength = length(lightGradient);
    
    if (surfaceGradientLength == 0)
    {
        return 1.0;
    }
    
    lightGradient /= lightGradientLength;
    
    float lightMult = 1.0 + clamp(
        NormalMapStrength * dot(lightGradient, surfaceGradient),
        -0.9,
        0.9
    );
    return lerp(
        1.0,
        lightMult,
        saturate(surfaceGradientLength)
    );
}

/* Vertex shaders ***********************************************************************/

void CloudShading_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    inout float4 color : COLOR0,
    out float4 screenPos : SV_Position
)
{
    screenPos = mul(position, MatrixTransform);
    color.rgb *= BaseColor;
}

void ExtractLuminance_VS(
    float4 position : POSITION0,
    inout float2 texCoord : TEXCOORD0,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    texCoord = Scale * (texCoord - 0.5) + 0.5;
}

void GenerateGradients_VS(
    float4 position : POSITION0,
    float2 texCoord : TEXCOORD0,
    out float2 luminanceTexCoord : TEXCOORD0,
    out float2 cloudTexCoord : TEXCOORD1,
    out float4 screenPos : SV_Position
)
{
    screenPos = position;
    luminanceTexCoord = Scale * (texCoord - 0.5) + 0.5;
    cloudTexCoord = CloudScale * (texCoord - 0.5) + 0.5;
}

/* Pixel shaders ************************************************************************/

float4 ExtractLuminance_PS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, texCoord);
    if (
        texCoord.x < 0
        || texCoord.x > 1
        || texCoord.y < 0
        || texCoord.y > 1
    )
    {
        texColor = float4(0, 0, 0, 1);
    }
    float luminance = saturate(Luminance(max(texColor.rgb, 0)));
    return float4(luminance, 0, 0, 1);
}

float4 GenerateGradients_PS(
    float2 luminanceTexCoord : TEXCOORD0,
    float2 cloudTexCoord : TEXCOORD1
) : COLOR0
{
    float blurredLuminance = tex2D(LuminanceSampler, luminanceTexCoord).r;
    float4 cloudColor = tex2D(CloudSampler, cloudTexCoord);
    if (
        cloudTexCoord.x < 0
        || cloudTexCoord.x > 1
        || cloudTexCoord.y < 0
        || cloudTexCoord.y > 1
    )
    {
        cloudColor = float4(0, 0, 0, 0);
    }
    
    float2 luminanceGradient = 20.0 * float2(
        ddx(blurredLuminance),
        ddy(blurredLuminance)
    );
    luminanceGradient = smoothstep(-1.0, 1.0, luminanceGradient);
    float cloudLuma = pow(saturate(Luminance(max(cloudColor.rgb, 0))), InverseGamma);
    return float4(luminanceGradient, cloudLuma, cloudColor.a);
}

float4 CloudShading_PS(float2 texCoord : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, texCoord);
    float2 surfaceGradient = -2 * texColor.xy + 1;
    
    float mult = NormalsMultiplierFancySky(surfaceGradient, texCoord);
    return color * texColor.a * float4(
        lerp(
            texColor.z,
            texColor.a * pow(
                0.8 * mult,
                InverseGamma
            ),
            SkyLightMult
        ).xxx,
        texColor.a
    );
}

/* Techniques ***************************************************************************/

technique ExtractLuminance
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 ExtractLuminance_VS();
        PixelShader = compile ps_3_0 ExtractLuminance_PS();
    }
}

technique GenerateGradients
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 GenerateGradients_VS();
        PixelShader = compile ps_3_0 GenerateGradients_PS();
    }
}

technique CloudShading
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 CloudShading_VS();
        PixelShader = compile ps_3_0 CloudShading_PS();
    }
}
