using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Graphics.Light;
using Vec2 = System.Numerics.Vector2;

namespace FancyLighting.LightingEngines;

internal sealed class EnhancedFancyLightingEngine : FancyLightingEngineBase<Vec2>
{
    private readonly record struct LightingSpread(
        int DistanceToTop,
        int DistanceToRight,
        Vec2 LightFromLeft,
        Vec2 LightFromBottom,
        Vec2 TopFromLeftX,
        Vec2 TopFromLeftY,
        Vec2 TopFromBottomX,
        Vec2 TopFromBottomY,
        Vec2 RightFromLeftX,
        Vec2 RightFromLeftY,
        Vec2 RightFromBottomX,
        Vec2 RightFromBottomY
    );

    private readonly record struct DistanceCache(double Top, double Right);

    private const int MAX_LIGHT_RANGE = 64;
    private const int DISTANCE_TICKS = 256;
    private const int MAX_DISTANCE = DISTANCE_TICKS;
    private float _logBrightnessCutoff;
    private float _reciprocalLogSlowestDecay;

    private float _lightLossExitingSolid;

    private const float LOW_LIGHT_LEVEL = 0.03f;
    private const float GI_MULT = 0.55f;

    private readonly LightingSpread[] _lightingSpread;

    private Vector3[] _tmp;
    private bool[] _skipGI;

    private int _temporalData;
    private bool _countTemporal;
    private bool _reduceCulling;

    public EnhancedFancyLightingEngine()
    {
        ComputeLightingSpread(out _lightingSpread);

        _lightAirDecay = new float[MAX_DISTANCE + 1];
        _lightSolidDecay = new float[MAX_DISTANCE + 1];
        _lightWaterDecay = new float[MAX_DISTANCE + 1];
        _lightHoneyDecay = new float[MAX_DISTANCE + 1];
        _lightShadowPaintDecay = new float[MAX_DISTANCE + 1];
        for (int exponent = 0; exponent <= MAX_DISTANCE; ++exponent)
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
                        + (Vec2.Dot(value.TopFromLeftX, Vec2.One) + Vec2.Dot(value.TopFromLeftY, Vec2.One)) / 2.0
                            * distances[row].Right
                        + (Vec2.Dot(value.TopFromBottomX, Vec2.One) + Vec2.Dot(value.TopFromBottomY, Vec2.One)) / 2.0
                            * distances[row - 1].Top,
                    value.DistanceToRight / (double)DISTANCE_TICKS
                        + (Vec2.Dot(value.RightFromLeftX, Vec2.One) + Vec2.Dot(value.RightFromLeftY, Vec2.One)) / 2.0
                            * distances[row].Right
                        + (Vec2.Dot(value.RightFromBottomX, Vec2.One) + Vec2.Dot(value.RightFromBottomY, Vec2.One)) / 2.0
                            * distances[row - 1].Top
                );
            }
        }
    }

    private static LightingSpread CalculateTileLightingSpread(
        int row, int col, double leftDistanceError, double bottomDistanceError
    )
    {
        static int DoubleToIndex(double x)
            => Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, MAX_DISTANCE);

        double distance = MathUtil.Hypot(col, row);
        double distanceToTop = MathUtil.Hypot(col, row + 1) - distance;
        double distanceToRight = MathUtil.Hypot(col + 1, row) - distance;

        if (row == 0 && col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused, but are accurate should they be used
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero
            );
        }

        if (row == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused, but are accurate should they be used
                new(0.5f, 0.5f),
                Vec2.Zero,
                Vec2.Zero,
                Vec2.One,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.UnitX,
                Vec2.UnitY,
                Vec2.Zero,
                Vec2.Zero
            );
        }

        if (col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused, but are accurate should they be used
                Vec2.Zero,
                new(0.5f, 0.5f),
                Vec2.Zero,
                Vec2.Zero,
                Vec2.UnitX,
                Vec2.UnitY,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.Zero,
                Vec2.One
            );
        }

        Span<double> lightFrom = stackalloc double[16];
        Span<double> area = stackalloc double[4];

        double leftX = col - 0.5;
        double midX = col;
        double rightX = col + 0.5;
        double bottomY = row - 0.5;
        double midY = row;
        double topY = row + 0.5;
        Span<double> x = stackalloc[] { leftX, leftX, midX, rightX };
        Span<double> y = stackalloc[] { midY, bottomY, bottomY, bottomY };
        double previousT = 0.0;
        for (int i = 0; i < 4; ++i)
        {
            double x1 = x[i];
            double y1 = y[i];

            double slope = y1 / x1;

            double t;
            double x2 = rightX;
            double y2 = y1 + (x2 - x1) * slope;
            if (y2 > topY)
            {
                y2 = topY;
                x2 = x1 + (y2 - y1) / slope;
                t = 2.0 * (x2 - leftX);
            }
            else
            {
                t = 2.0 * (topY - y2) + 2.0;
            }

            area[i] = (topY - y1) * (x2 - leftX) - 0.5 * (y2 - y1) * (x2 - x1);

            for (int j = 0; j < 4; ++j)
            {
                int index = 4 * i + j;

                if (j + 1 <= previousT)
                {
                    lightFrom[index] = 0.0;
                    continue;
                }
                if (j >= t)
                {
                    lightFrom[index] = 0.0;
                    continue;
                }

                double value = j < previousT ? j + 1 - previousT : 1.0;
                value -= j + 1 > t ? j + 1 - t : 0.0;
                lightFrom[index] = value;
            }

            previousT = t;
        }

        distanceToTop
            -= (lightFrom[0] + lightFrom[1] + lightFrom[4] + lightFrom[5]) * leftDistanceError
            + (lightFrom[8] + lightFrom[9] + lightFrom[12] + lightFrom[13]) * bottomDistanceError;
        distanceToRight
            -= (lightFrom[2] + lightFrom[3] + lightFrom[6] + lightFrom[7]) * leftDistanceError
            + (lightFrom[10] + lightFrom[11] + lightFrom[14] + lightFrom[15]) * bottomDistanceError;

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            new((float)(area[1] - area[0]), (float)area[0]),
            new((float)(area[2] - area[1]), (float)(area[3] - area[2])),
            new((float)lightFrom[4], (float)lightFrom[5]),
            new((float)lightFrom[0], (float)lightFrom[1]),
            new((float)lightFrom[8], (float)lightFrom[9]),
            new((float)lightFrom[12], (float)lightFrom[13]),
            new((float)lightFrom[7], (float)lightFrom[6]),
            new((float)lightFrom[3], (float)lightFrom[2]),
            new((float)lightFrom[11], (float)lightFrom[10]),
            new((float)lightFrom[15], (float)lightFrom[14])
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
        _lightLossExitingSolid = LightingConfig.Instance.FancyLightingEngineExitMultiplier();

        _logBrightnessCutoff = FancyLightingMod._inCameraMode
            ? 0.02f
            : LightingConfig.Instance.FancyLightingEngineUseTemporal
                ? (float)Math.Clamp(Math.Sqrt(_temporalData / 55555.5) * 0.02, 0.02, 0.125)
                : 0.04f;
        _logBrightnessCutoff = MathF.Log(_logBrightnessCutoff);
        _temporalData = 0;

        const float MAX_DECAY_VALUE = 0.97f;
        UpdateDecays(lightMap, MAX_DECAY_VALUE, DISTANCE_TICKS);

        _reciprocalLogSlowestDecay = 1f / MathF.Log(
            Math.Max(
                Math.Max(_lightAirDecay[DISTANCE_TICKS], _lightSolidDecay[DISTANCE_TICKS]),
                Math.Max(_lightWaterDecay[DISTANCE_TICKS], _lightHoneyDecay[DISTANCE_TICKS])
            )
        );

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
        _reduceCulling = false;
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

            int xmin = _lightMapArea.X + 1;
            int xmax = _lightMapArea.X + width - 2;
            int ymin = _lightMapArea.Y + 1;
            int ymax = _lightMapArea.Y + height - 2;

            Parallel.For(
                0,
                width,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (i) =>
                {
                    int x = i + _lightMapArea.X;
                    int y = _lightMapArea.Y;
                    bool notOnLeft = x > xmin;
                    bool notOnRight = x < xmax;
                    int endIndex = height * (i + 1);
                    for (int j = height * i; j < endIndex; ++j, ++y)
                    {
                        ref Vector3 giLight = ref colors[j];

                        if (lightMasks[j] is LightMaskMode.Solid)
                        {
                            giLight.X = 0f;
                            giLight.Y = 0f;
                            giLight.Z = 0f;
                            _skipGI[j] = true;
                            continue;
                        }

                        Vector3 origLight = giLight;
                        ref Vector3 light = ref _tmp[j];
                        giLight.X = GI_MULT * light.X;
                        giLight.Y = GI_MULT * light.Y;
                        giLight.Z = GI_MULT * light.Z;

                        _skipGI[j]
                            = giLight.X <= origLight.X
                            && giLight.Y <= origLight.Y
                            && giLight.Z <= origLight.Z;
                    }
                }
            );

            _countTemporal = false;
            _reduceCulling = true;
            RunLightingPass(
                _tmp,
                colors,
                length,
                (workingLightMap, workingLights, i) =>
                {
                    if (lightMasks[i] is LightMaskMode.Solid)
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
        if (color.X <= LOW_LIGHT_LEVEL && color.Y <= LOW_LIGHT_LEVEL && color.Z <= LOW_LIGHT_LEVEL)
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

        float reciprocalThreshold = 1f / (_lightMask[index][MAX_DISTANCE] * _lightMask[index][MAX_DISTANCE / 2]);
        int length = width * height;

        int midX = index / height;
        int topEdge = height * midX;
        int bottomEdge = topEdge + (height - 1);

        bool skipUp, skipDown, skipLeft, skipRight;

        if (index == topEdge)
        {
            skipUp = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index - 1][DISTANCE_TICKS] * reciprocalThreshold * colors[index - 1];
            skipUp = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index == bottomEdge)
        {
            skipDown = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index + 1][DISTANCE_TICKS] * reciprocalThreshold * colors[index + 1];
            skipDown = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index < height)
        {
            skipLeft = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index - height][DISTANCE_TICKS] * reciprocalThreshold * colors[index - height];
            skipLeft = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index + height >= length)
        {
            skipRight = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index + height][DISTANCE_TICKS] * reciprocalThreshold * colors[index + height];
            skipRight = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
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
        bool doUpperRight = _reduceCulling ? !(skipUp && skipRight) : !(skipUp || skipRight);
        bool doUpperLeft = _reduceCulling ? !(skipUp && skipLeft) : !(skipUp || skipLeft);
        bool doLowerRight = _reduceCulling ? !(skipDown && skipRight) : !(skipDown || skipRight);
        bool doLowerLeft = _reduceCulling ? !(skipDown && skipLeft) : !(skipDown || skipLeft);

        int leftEdge = Math.Min(midX, lightRange);
        int rightEdge = Math.Min(width - 1 - midX, lightRange);
        topEdge = Math.Min(index - topEdge, lightRange);
        bottomEdge = Math.Min(bottomEdge - index, lightRange);

        int[] circle = _circles[lightRange];

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            void ProcessQuadrant(int edgeY, int edgeX, int indexVerticalChange, int indexHorizontalChange)
            {
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
                            Vec2.Dot(verticalLight, spread.LightFromBottom)
                            + Vec2.Dot(horizontalLight, spread.LightFromLeft)
                        );
                        workingLights[y]
                            = (
                                (
                                    horizontalLight.X * spread.RightFromLeftX
                                    + horizontalLight.Y * spread.RightFromLeftY
                                )
                                + (
                                    verticalLight.X * spread.RightFromBottomX
                                    + verticalLight.Y * spread.RightFromBottomY
                                )
                            ) * mask[spread.DistanceToRight];
                        verticalLight
                            = (
                                (
                                    horizontalLight.X * spread.TopFromLeftX
                                    + horizontalLight.Y * spread.TopFromLeftY
                                )
                                + (
                                    verticalLight.X * spread.TopFromBottomX
                                    + verticalLight.Y * spread.TopFromBottomY
                                )
                            ) * mask[spread.DistanceToTop];
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
                + (!(skipUp || skipRight) ? baseWork * baseWork : 0)
                + (!(skipUp || skipLeft) ? baseWork * baseWork : 0)
                + (!(skipDown || skipRight) ? baseWork * baseWork : 0)
                + (!(skipDown || skipLeft) ? baseWork * baseWork : 0);

            Interlocked.Add(ref _temporalData, approximateWorkDone);
        }
    }
}
