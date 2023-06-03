using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Terraria.Graphics.Light;
using Vec2 = System.Numerics.Vector2;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

internal sealed class EnhancedFancyLightingEngine : FancyLightingEngineBase<Vec2>
{
    private readonly record struct LightingSpread(
        int DistanceToTop,
        int DistanceToRight,
        Vec4 LightFrom,
        Vec4 FromLeftX,
        Vec4 FromLeftY,
        Vec4 FromBottomX,
        Vec4 FromBottomY
    );

    private readonly record struct DistanceCache(double Top, double Right);

    private const int MAX_LIGHT_RANGE = 64;
    private const int DISTANCE_TICKS = 256;

    private const float GI_MULT = 0.55f;

    private readonly LightingSpread[] _lightingSpread;

    private Vector3[] _tmp;
    private bool[] _skipGI;

    private bool _countTemporal;

    public EnhancedFancyLightingEngine()
    {
        ComputeLightingSpread(out _lightingSpread);

        _lightAirDecay = new float[DISTANCE_TICKS + 1];
        _lightSolidDecay = new float[DISTANCE_TICKS + 1];
        _lightWaterDecay = new float[DISTANCE_TICKS + 1];
        _lightHoneyDecay = new float[DISTANCE_TICKS + 1];
        _lightShadowPaintDecay = new float[DISTANCE_TICKS + 1];
        for (int exponent = 0; exponent <= DISTANCE_TICKS; ++exponent)
        {
            _lightAirDecay[exponent] = 1f;
            _lightSolidDecay[exponent] = 1f;
            _lightWaterDecay[exponent] = 1f;
            _lightHoneyDecay[exponent] = 1f;
            _lightShadowPaintDecay[exponent] = 0f;
        }

        ComputeCircles(MAX_LIGHT_RANGE);

        _temporalData = 0;
    }

    private void ComputeLightingSpread(out LightingSpread[] values)
    {
        values = new LightingSpread[(MAX_LIGHT_RANGE + 1) * (MAX_LIGHT_RANGE + 1)];
        DistanceCache[] distances = new DistanceCache[MAX_LIGHT_RANGE + 1];

        for (int row = 0; row <= MAX_LIGHT_RANGE; ++row)
        {
            int index = row;
            ref LightingSpread value = ref values[index];
            value = CalculateTileLightingSpread(row, 0, 0.0, 0.0);
            distances[row] = new(row + 1.0, row + value.DistanceToRight / (double)DISTANCE_TICKS);
        }

        for (int col = 1; col <= MAX_LIGHT_RANGE; ++col)
        {
            int index = (MAX_LIGHT_RANGE + 1) * col;
            ref LightingSpread value = ref values[index];
            value = CalculateTileLightingSpread(0, col, 0.0, 0.0);
            distances[0] = new(col + value.DistanceToTop / (double)DISTANCE_TICKS, col + 1.0);

            for (int row = 1; row <= MAX_LIGHT_RANGE; ++row)
            {
                ++index;
                double distance = MathUtil.Hypot(row, col);
                value = ref values[index];
                value = CalculateTileLightingSpread(
                    row, col, distances[row].Right - distance, distances[row - 1].Top - distance
                );

                distances[row] = new(
                    value.DistanceToTop / (double)DISTANCE_TICKS
                        + ((value.FromLeftX.X + value.FromLeftX.Y) + (value.FromLeftY.X + value.FromLeftY.Y))
                            / 2.0 * distances[row].Right
                        + ((value.FromBottomX.X + value.FromBottomX.Y) + (value.FromBottomY.X + value.FromBottomY.Y))
                            / 2.0 * distances[row - 1].Top,
                    value.DistanceToRight / (double)DISTANCE_TICKS
                        + ((value.FromLeftX.Z + value.FromLeftX.W) + (value.FromLeftY.Z + value.FromLeftY.W))
                            / 2.0 * distances[row].Right
                        + ((value.FromBottomX.Z + value.FromBottomX.W) + (value.FromBottomY.Z + value.FromBottomY.W))
                            / 2.0 * distances[row - 1].Top
                );
            }
        }
    }

    private static LightingSpread CalculateTileLightingSpread(
        int row, int col, double leftDistanceError, double bottomDistanceError
    )
    {
        static int DoubleToIndex(double x)
            => Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, DISTANCE_TICKS);

        double distance = MathUtil.Hypot(col, row);
        double distanceToTop = MathUtil.Hypot(col, row + 1) - distance;
        double distanceToRight = MathUtil.Hypot(col + 1, row) - distance;

        if (row == 0 && col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero
            );
        }

        if (row == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero
            );
        }

        if (col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero,
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero
            );
        }

        Span<double> lightFrom = stackalloc double[4 * 4];
        Span<double> area = stackalloc double[4];

        Span<double> x = stackalloc[] { 0.0, 0.0, 0.5, 1.0 };
        Span<double> y = stackalloc[] { 0.5, 0.0, 0.0, 0.0 };
        CalculateSubTileLightingSpread(in x, in y, ref lightFrom, ref area, row, col);

        distanceToTop
            -= (lightFrom[0] + lightFrom[1] + lightFrom[4] + lightFrom[5]) / 2.0 * leftDistanceError
            + (lightFrom[8] + lightFrom[9] + lightFrom[12] + lightFrom[13]) / 2.0 * bottomDistanceError;
        distanceToRight
            -= (lightFrom[2] + lightFrom[3] + lightFrom[6] + lightFrom[7]) / 2.0 * leftDistanceError
            + (lightFrom[10] + lightFrom[11] + lightFrom[14] + lightFrom[15]) / 2.0 * bottomDistanceError;

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            new((float)(area[1] - area[0]), (float)area[0], (float)(area[2] - area[1]), (float)(area[3] - area[2])),
            new((float)lightFrom[4], (float)lightFrom[5], (float)lightFrom[7], (float)lightFrom[6]),
            new((float)lightFrom[0], (float)lightFrom[1], (float)lightFrom[3], (float)lightFrom[2]),
            new((float)lightFrom[8], (float)lightFrom[9], (float)lightFrom[11], (float)lightFrom[10]),
            new((float)lightFrom[12], (float)lightFrom[13], (float)lightFrom[15], (float)lightFrom[14])
        );
    }

    public override void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        UpdateBrightnessCutoff();

        UpdateDecays(lightMap, DISTANCE_TICKS);

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            ConvertLightColorsToLinear(colors, width, height);
        }

        int length = width * height;

        if (_tmp is null || _tmp.Length < length)
        {
            _tmp = new Vector3[length];
            _lightMask = new float[length][];
        }

        Array.Copy(colors, _tmp, length);

        UpdateLightMasks(lightMasks, width, height);

        InitializeTaskVariables(length, MAX_LIGHT_RANGE);

        bool doGI = LightingConfig.Instance.SimulateGlobalIllumination;

        _countTemporal = true;
        RunLightingPass(
            colors,
            doGI ? _tmp : colors,
            length,
            (workingLightMap, workingLights, i)
                => ProcessLight(workingLightMap, workingLights, i, colors, width, height)
        );

        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            if (_skipGI is null || _skipGI.Length < length)
            {
                _skipGI = new bool[length];
            }

            GetLightsForGlobalIllumination(
                _tmp, colors, colors, _skipGI, lightMasks, width, height, GI_MULT
            );

            _countTemporal = false;
            RunLightingPass(
                _tmp,
                colors,
                length,
                (workingLightMap, workingLights, i) =>
                {
                    if (_skipGI[i])
                    {
                        return;
                    }

                    ProcessLight(workingLightMap, workingLights, i, colors, width, height);
                }
            );
        }
    }

    private void ProcessLight(
        Vector3[] workingLightMap, Vec2[] workingLights, int index, Vector3[] colors, int width, int height
    )
    {
        Vector3 color = colors[index];
        if (
            color.X <= _initialBrightnessCutoff
            && color.Y <= _initialBrightnessCutoff
            && color.Z <= _initialBrightnessCutoff
        )
        {
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetLightMap(int i, float value)
        {
            ref Vector3 vec = ref workingLightMap[i];

            // Potentially faster than Math.Max, which checks for NaN and signed zero

            float newValue;

            newValue = value * color.X;
            if (newValue > vec.X)
            {
                vec.X = newValue;
            }

            newValue = value * color.Y;
            if (newValue > vec.Y)
            {
                vec.Y = newValue;
            }

            newValue = value * color.Z;
            if (newValue > vec.Z)
            {
                vec.Z = newValue;
            }
        }

        int length = width * height;

        int midX = index / height;
        int topEdge = height * midX;
        int bottomEdge = topEdge + (height - 1);

        bool skipUp, skipDown, skipLeft, skipRight;

        Vector3.Multiply(
            ref color,
            _lightMask[index][DISTANCE_TICKS] * _lightMask[index][DISTANCE_TICKS / 2],
            out Vector3 threshold
        );

        if (index == topEdge)
        {
            skipUp = true;
        }
        else
        {
            Vector3.Multiply(ref colors[index - 1], _lightMask[index - 1][DISTANCE_TICKS], out Vector3 otherColor);
            skipUp = otherColor.X >= threshold.X && otherColor.Y >= threshold.Y && otherColor.Z >= threshold.Z;
        }
        if (index == bottomEdge)
        {
            skipDown = true;
        }
        else
        {
            Vector3.Multiply(ref colors[index + 1], _lightMask[index + 1][DISTANCE_TICKS], out Vector3 otherColor);
            skipDown = otherColor.X >= threshold.X && otherColor.Y >= threshold.Y && otherColor.Z >= threshold.Z;
        }
        if (index < height)
        {
            skipLeft = true;
        }
        else
        {
            Vector3.Multiply(ref colors[index - height], _lightMask[index - height][DISTANCE_TICKS], out Vector3 otherColor);
            skipLeft = otherColor.X >= threshold.X && otherColor.Y >= threshold.Y && otherColor.Z >= threshold.Z;
        }
        if (index + height >= length)
        {
            skipRight = true;
        }
        else
        {
            Vector3.Multiply(ref colors[index + height], _lightMask[index + height][DISTANCE_TICKS], out Vector3 otherColor);
            skipRight = otherColor.X >= threshold.X && otherColor.Y >= threshold.Y && otherColor.Z >= threshold.Z;
        }

        // We blend by taking the max of each component, so this is a valid check to skip
        if (skipUp && skipDown && skipLeft && skipRight)
        {
            return;
        }

        float initialDecay = _lightMask[index][DISTANCE_TICKS];
        int lightRange = Math.Clamp(
            (int)Math.Ceiling(
                (
                    _logBrightnessCutoff
                    - MathF.Log(initialDecay * Math.Max(color.X, Math.Max(color.Y, color.Z)))
                ) * _reciprocalLogSlowestDecay
            ) + 1,
            1,
            MAX_LIGHT_RANGE
        );

        // Up
        if (!skipUp)
        {
            float lightValue = 1f;
            int i = index;
            for (int y = 1; y <= lightRange; ++y)
            {
                if (--i < topEdge)
                {
                    break;
                }

                lightValue *= _lightMask[i + 1][DISTANCE_TICKS];
                if (y > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                {
                    lightValue *= _lightLossExitingSolid;
                }

                SetLightMap(i, lightValue);
            }
        }

        // Down
        if (!skipDown)
        {
            float lightValue = 1f;
            int i = index;
            for (int y = 1; y <= lightRange; ++y)
            {
                if (++i > bottomEdge)
                {
                    break;
                }

                lightValue *= _lightMask[i - 1][DISTANCE_TICKS];
                if (y > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                {
                    lightValue *= _lightLossExitingSolid;
                }

                SetLightMap(i, lightValue);
            }
        }

        // Left
        if (!skipLeft)
        {
            float lightValue = 1f;
            int i = index;
            for (int x = 1; x <= lightRange; ++x)
            {
                if ((i -= height) < 0)
                {
                    break;
                }

                lightValue *= _lightMask[i + height][DISTANCE_TICKS];
                if (x > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i + height] == _lightSolidDecay)
                {
                    lightValue *= _lightLossExitingSolid;
                }

                SetLightMap(i, lightValue);
            }
        }

        // Right
        if (!skipRight)
        {
            float lightValue = 1f;
            int i = index;
            for (int x = 1; x <= lightRange; ++x)
            {
                if ((i += height) >= length)
                {
                    break;
                }

                lightValue *= _lightMask[i - height][DISTANCE_TICKS];
                if (x > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i - height] == _lightSolidDecay)
                {
                    lightValue *= _lightLossExitingSolid;
                }

                SetLightMap(i, lightValue);
            }
        }

        // Using || instead of && for culling is sometimes inaccurate, but much faster
        bool doUpperRight = !(skipUp || skipRight);
        bool doUpperLeft = !(skipUp || skipLeft);
        bool doLowerRight = !(skipDown || skipRight);
        bool doLowerLeft = !(skipDown || skipLeft);

        int leftEdge = Math.Min(midX, lightRange);
        int rightEdge = Math.Min(width - 1 - midX, lightRange);
        topEdge = Math.Min(index - topEdge, lightRange);
        bottomEdge = Math.Min(bottomEdge - index, lightRange);

        int[] circle = _circles[lightRange];

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            void ProcessQuadrant(int edgeY, int edgeX, int indexVerticalChange, int indexHorizontalChange)
            {
                // Performance optimization
                float[][] _lightMask = this._lightMask;
                float[] _lightAirDecay = this._lightAirDecay;
                float[] _lightSolidDecay = this._lightSolidDecay;
                float _lightLossExitingSolid = this._lightLossExitingSolid;
                LightingSpread[] _lightingSpread = this._lightingSpread;

                workingLights[0] = new(initialDecay);
                float value = 1f;
                for (int i = index, y = 1; y <= edgeY; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[i += indexVerticalChange];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i - indexVerticalChange] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = new(value * mask[_lightingSpread[y].DistanceToRight]);
                }
                for (int x = 1; x <= edgeX; ++x)
                {
                    int i = index + indexHorizontalChange * x;
                    float[] mask = _lightMask[i];

                    int j = (MAX_LIGHT_RANGE + 1) * x;
                    Vec2 verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[j].DistanceToTop];
                    workingLights[0] *= mask[DISTANCE_TICKS];
                    if (x > 1 && mask == _lightAirDecay && _lightMask[i - indexHorizontalChange] == _lightSolidDecay)
                    {
                        verticalLight *= _lightLossExitingSolid;
                        workingLights[0] *= _lightLossExitingSolid;
                    }

                    int edge = Math.Min(edgeY, circle[x]);
                    for (int y = 1; y <= edge; ++y)
                    {
                        mask = _lightMask[i += indexVerticalChange];
                        Vec2 horizontalLight = workingLights[y];

                        if (mask == _lightAirDecay)
                        {
                            if (_lightMask[i - indexVerticalChange] == _lightSolidDecay)
                            {
                                verticalLight *= _lightLossExitingSolid;
                            }

                            if (_lightMask[i - indexHorizontalChange] == _lightSolidDecay)
                            {
                                horizontalLight *= _lightLossExitingSolid;
                            }
                        }
                        ref LightingSpread spread
                            = ref _lightingSpread[++j];
                        SetLightMap(i,
                            Vec4.Dot(
                                new Vec4(horizontalLight, verticalLight.X, verticalLight.Y),
                                spread.LightFrom
                            )
                        );
                        float topDecay = mask[spread.DistanceToTop];
                        float rightDecay = mask[spread.DistanceToRight];
                        Vec4 outgoingLight = (
                                (
                                    horizontalLight.X * spread.FromLeftX
                                    + horizontalLight.Y * spread.FromLeftY
                                )
                                + (
                                    verticalLight.X * spread.FromBottomX
                                    + verticalLight.Y * spread.FromBottomY
                                )
                            ) * new Vec4(topDecay, topDecay, rightDecay, rightDecay);
                        workingLights[y] = new(outgoingLight.Z, outgoingLight.W);
                        verticalLight = new(outgoingLight.X, outgoingLight.Y);
                    }
                }
            }

            if (doUpperRight)
            {
                ProcessQuadrant(topEdge, rightEdge, -1, height);
            }

            if (doUpperLeft)
            {
                ProcessQuadrant(topEdge, leftEdge, -1, -height);
            }

            if (doLowerRight)
            {
                ProcessQuadrant(bottomEdge, rightEdge, 1, height);
            }

            if (doLowerLeft)
            {
                ProcessQuadrant(bottomEdge, leftEdge, 1, -height);
            }
        }

        if (_countTemporal)
        {
            const float LOG_BASE_DECAY = -3.218876f; // log(0.04)

            int baseWork = Math.Clamp(
                (int)Math.Ceiling(
                    (
                        LOG_BASE_DECAY
                        - MathF.Log(initialDecay * Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) * _reciprocalLogSlowestDecay
                ) + 1,
                1,
                MAX_LIGHT_RANGE
            );

            int approximateWorkDone
                = 1
                + (!skipUp ? baseWork : 0)
                + (!skipDown ? baseWork : 0)
                + (!skipLeft ? baseWork : 0)
                + (!skipRight ? baseWork : 0)
                + (doUpperRight ? baseWork * baseWork : 0)
                + (doUpperLeft ? baseWork * baseWork : 0)
                + (doLowerRight ? baseWork * baseWork : 0)
                + (doLowerLeft ? baseWork * baseWork : 0);

            Interlocked.Add(ref _temporalData, approximateWorkDone);
        }
    }
}
