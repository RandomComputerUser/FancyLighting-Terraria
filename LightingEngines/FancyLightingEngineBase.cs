using FancyLighting.Config;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;

namespace FancyLighting.LightingEngines;

internal abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;

    protected float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    protected float[] _lightWaterDecay;
    protected float[] _lightHoneyDecay;
    protected float[] _lightShadowPaintDecay; // In vanilla shadow paint isn't a special case

    protected float[][] _lightMask;

    public void Unload()
    { }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    );

    protected void ComputeCircles(int maxLightRange)
    {
        _circles = new int[maxLightRange + 1][];
        _circles[0] = new int[] { 0 };
        for (int radius = 1; radius < maxLightRange + 1; ++radius)
        {
            _circles[radius] = new int[radius + 1];
            _circles[radius][0] = radius;
            double diagonal = radius / Math.Sqrt(2.0);
            for (int x = 1; x <= radius; ++x)
            {
                _circles[radius][x] = x <= diagonal
                    ? (int)Math.Ceiling(Math.Sqrt(radius * radius - x * x))
                    : (int)Math.Floor(Math.Sqrt(radius * radius - (x - 1) * (x - 1)));
            }
        }
    }

    protected void UpdateDecay(float[] decay, float baseline, int exponentDivisor)
    {
        if (baseline == decay[exponentDivisor])
        {
            return;
        }

        float logBaseline = MathF.Log(baseline);
        float exponentMult = 1f / exponentDivisor;
        for (int i = 0; i < decay.Length; ++i)
        {
            decay[i] = MathF.Exp(exponentMult * i * logBaseline);
        }
    }

    protected void UpdateDecays(
        LightMap lightMap,
        float maxDecayMult,
        int exponentDivisor
    )
    {
        float decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
        float lightAirDecayBaseline
            = decayMult * Math.Min(lightMap.LightDecayThroughAir, maxDecayMult);
        float lightSolidDecayBaseline = decayMult * Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            maxDecayMult
        );
        float lightWaterDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughWater.X,
                Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
            ),
            maxDecayMult
        );
        float lightHoneyDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughHoney.X,
                Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
            ),
            maxDecayMult
        );

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline, exponentDivisor);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline, exponentDivisor);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline, exponentDivisor);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline, exponentDivisor);
    }

    protected void UpdateLightMasks(
        LightMaskMode[] lightMasks, int width, int height
    ) => Parallel.For(
        0,
        width,
        new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
        (i) =>
        {
            int x = i + _lightMapArea.X;
            int y = _lightMapArea.Y;
            int endIndex = height * (i + 1);
            for (int j = height * i; j < endIndex; ++j)
            {
                _lightMask[j] = lightMasks[j] switch
                {
                    LightMaskMode.Solid
                        => Main.tile[x, y].TileColor == PaintID.ShadowPaint
                            ? _lightShadowPaintDecay
                            : _lightSolidDecay,
                    LightMaskMode.Water => _lightWaterDecay,
                    LightMaskMode.Honey => _lightHoneyDecay,
                    _ => _lightAirDecay,
                };
                ++y;
            }
        }
    );
}
