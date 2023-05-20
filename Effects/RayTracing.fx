sampler LightSampler : register(s0);
sampler NoiseSampler : register(s1);

float2 LightmapSize;
float2 ReciprocalLightmapSize;

float SolidThreshold;
float SolidExitLightLoss;

float2 NoiseCoordMult;
float NoiseLerp;
float2 Offset1;
float2 Offset2;

// 254 is the max allowed, but 252 is divisible by 4
#define NUM_RAYS 252
#define PI 3.1415927f

float4 RayTracedLighting(float2 coords : TEXCOORD0) : COLOR0
{
    float4 thisLight = tex2D(LightSampler, coords, 0, 0);

    float2 thisTileCenter = floor(LightmapSize * coords) + float2(0.5, 0.5);

    float3 result = thisLight.rgb;

    [loop]
    for (int ray = 0; ray < NUM_RAYS; ++ray)
    {
        float noise1 = tex2D(NoiseSampler, NoiseCoordMult * coords + Offset1).r;
        float noise2 = tex2D(NoiseSampler, NoiseCoordMult * coords + Offset2).r;
        float angle = (ray + lerp(noise1, noise2, NoiseLerp)) * (2 * PI / NUM_RAYS);

        float dx = cos(angle);
        float dy = sin(angle);
        float reciprocal_dx = 1 / dx;
        float reciprocal_dy = 1 / dy;

        float t = 0;
        float2 position = thisTileCenter;
        float4 previousLight = thisLight;
        float multiplier = 1;
        while (multiplier > 0.03)
        {
            float next_x = dx > 0 ? floor(position.x + 1) : ceil(position.x - 1);
            float dt_x = dx == 0 ? 1000 : reciprocal_dx * (next_x - position.x);

            float next_y = dy > 0 ? floor(position.y + 1) : ceil(position.y - 1);
            float dt_y = dy == 0 ? 1000 : reciprocal_dy * (next_y - position.y);

            float2 texCoord;

            if (dt_x < dt_y)
            {
                t += dt_x;
                multiplier *= pow(previousLight.a, dt_x);
                position.y += dt_x * dy;
                texCoord.y = position.y;
                position.x = next_x;
                if (dx > 0)
                {
                    if (position.x >= LightmapSize.x)
                    {
                        break;
                    }
                    texCoord.x = position.x + 0.5;
                }
                else
                {
                    if (position.x <= 0)
                    {
                        break;
                    }
                    texCoord.x = position.x - 0.5;
                }
            }
            else
            {
                t += dt_y;
                multiplier *= pow(previousLight.a, dt_y);
                position.x += dt_y * dx;
                texCoord.x = position.x;
                position.y = next_y;
                if (dy > 0)
                {
                    if (position.y >= LightmapSize.y)
                    {
                        break;
                    }
                    texCoord.y = position.y + 0.5;
                }
                else
                {
                    if (position.y <= 0)
                    {
                        break;
                    }
                    texCoord.y = position.y - 0.5;
                }
            }

            texCoord *= ReciprocalLightmapSize;

            float4 light = tex2D(LightSampler, texCoord, 0, 0);

            if (previousLight.a > SolidThreshold && light.a < SolidThreshold)
            {
                multiplier *= SolidExitLightLoss;
            }

            result = max(result, multiplier * light.rgb);

            previousLight = light;
        }
    }

    return float4(result, 1);
}

technique Technique1
{
    pass RayTracedLighting
    {
        PixelShader = compile ps_3_0 RayTracedLighting();
    }
}
