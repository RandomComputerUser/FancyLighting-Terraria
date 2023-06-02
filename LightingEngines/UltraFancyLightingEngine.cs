using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Graphics.Light;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

internal sealed class UltraFancyLightingEngine : FancyLightingEngineBase<Vec4>
{
    private readonly record struct LightingSpread(
        int DistanceToTop,
        int DistanceToRight,
        Vec4 LightFromLeft,
        Vec4 LightFromBottom,
        Vec4 TopFromLeftX,
        Vec4 TopFromLeftY,
        Vec4 TopFromLeftZ,
        Vec4 TopFromLeftW,
        Vec4 TopFromBottomX,
        Vec4 TopFromBottomY,
        Vec4 TopFromBottomZ,
        Vec4 TopFromBottomW,
        Vec4 RightFromLeftX,
        Vec4 RightFromLeftY,
        Vec4 RightFromLeftZ,
        Vec4 RightFromLeftW,
        Vec4 RightFromBottomX,
        Vec4 RightFromBottomY,
        Vec4 RightFromBottomZ,
        Vec4 RightFromBottomW
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

    public UltraFancyLightingEngine()
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
        double[] lightFrom = new double[8 * 8];

        for (int row = 0; row <= MAX_LIGHT_RANGE; ++row)
        {
            int index = row;
            ref LightingSpread value = ref values[index];
            value = CalculateTileLightingSpread(lightFrom, row, 0, 0.0, 0.0);
            distances[row] = new(row + 1.0, row + value.DistanceToRight / (double)DISTANCE_TICKS);
        }

        for (int col = 1; col <= MAX_LIGHT_RANGE; ++col)
        {
            int index = (MAX_LIGHT_RANGE + 1) * col;
            ref LightingSpread value = ref values[index];
            value = CalculateTileLightingSpread(lightFrom, 0, col, 0.0, 0.0);
            distances[0] = new(col + value.DistanceToTop / (double)DISTANCE_TICKS, col + 1.0);

            for (int row = 1; row <= MAX_LIGHT_RANGE; ++row)
            {
                ++index;
                double distance = MathUtil.Hypot(row, col);
                value = ref values[index];
                value = CalculateTileLightingSpread(
                    lightFrom, row, col, distances[row].Right - distance, distances[row - 1].Top - distance
                );

                distances[row] = new(
                    value.DistanceToTop / (double)DISTANCE_TICKS
                        + (
                            Vec4.Dot(value.TopFromLeftX, Vec4.One)
                            + Vec4.Dot(value.TopFromLeftY, Vec4.One)
                            + Vec4.Dot(value.TopFromLeftZ, Vec4.One)
                            + Vec4.Dot(value.TopFromLeftW, Vec4.One)
                        ) / 4.0 * distances[row].Right
                        + (
                            Vec4.Dot(value.TopFromBottomX, Vec4.One)
                            + Vec4.Dot(value.TopFromBottomY, Vec4.One)
                            + Vec4.Dot(value.TopFromBottomZ, Vec4.One)
                            + Vec4.Dot(value.TopFromBottomW, Vec4.One)
                        ) / 4.0 * distances[row - 1].Top,
                    value.DistanceToRight / (double)DISTANCE_TICKS
                        + (
                            Vec4.Dot(value.RightFromLeftX, Vec4.One)
                            + Vec4.Dot(value.RightFromLeftY, Vec4.One)
                            + Vec4.Dot(value.RightFromLeftZ, Vec4.One)
                            + Vec4.Dot(value.RightFromLeftW, Vec4.One)
                        ) / 4.0 * distances[row].Right
                        + (
                            Vec4.Dot(value.RightFromBottomX, Vec4.One)
                            + Vec4.Dot(value.RightFromBottomY, Vec4.One)
                            + Vec4.Dot(value.RightFromBottomZ, Vec4.One)
                            + Vec4.Dot(value.RightFromBottomW, Vec4.One)
                        ) / 4.0 * distances[row - 1].Top
                );
            }
        }
    }

    private static LightingSpread CalculateTileLightingSpread(
        double[] lightFrom, int row, int col, double leftDistanceError, double bottomDistanceError
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
                // The values below are unused and should never be used
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero
            );
        }

        if (row == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero
            );
        }

        if (col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero,
                Vec4.Zero, Vec4.Zero, Vec4.Zero, Vec4.Zero
            );
        }

        Span<double> area = stackalloc double[8];

        double xLeft = col - 0.5;
        double xMidLeft = col - 0.25;
        double xMid = col;
        double xMidRight = col + 0.25;
        double xRight = col + 0.5;
        double yBottom = row - 0.5;
        double yMidBottom = row - 0.25;
        double yMid = row;
        double yMidTop = row + 0.25;
        double yTop = row + 0.5;
        Span<double> x = stackalloc[] { xLeft, xLeft, xLeft, xLeft, xMidLeft, xMid, xMidRight, xRight };
        Span<double> y = stackalloc[] { yMidTop, yMid, yMidBottom, yBottom, yBottom, yBottom, yBottom, yBottom };
        double previousT = 0.0;
        for (int i = 0; i < 8; ++i)
        {
            double x1 = x[i];
            double y1 = y[i];

            double slope = y1 / x1;

            double t;
            double x2 = xRight;
            double y2 = y1 + (x2 - x1) * slope;
            if (y2 > yTop)
            {
                y2 = yTop;
                x2 = x1 + (y2 - y1) / slope;
                t = 4.0 * (x2 - xLeft);
            }
            else
            {
                t = 4.0 * ((yTop - y2) + 1.0);
            }

            area[i] = (yTop - y1) * (x2 - xLeft) - 0.5 * (y2 - y1) * (x2 - x1);

            for (int j = 0; j < 8; ++j)
            {
                int index = 8 * i + j;

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

        double QuadrantSum(int index)
        {
            double result = 0.0;
            int i = index;
            for (int row = 0; row < 4; ++row)
            {
                for (int col = 0; col < 4; ++col)
                {
                    result += lightFrom[i++];
                }
                i += 4;
            }

            return result;
        }

        Vec4 VectorAt(int index)
            => new(
                (float)lightFrom[index],
                (float)lightFrom[index + 1],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 3]
            );

        Vec4 ReverseVectorAt(int index)
            => new(
                (float)lightFrom[index + 3],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 1],
                (float)lightFrom[index]
            );

        distanceToTop
            -= QuadrantSum(8 * 0 + 0) / 4.0 * leftDistanceError
            + QuadrantSum(8 * 4 + 0) / 4.0 * bottomDistanceError;
        distanceToRight
            -= QuadrantSum(8 * 0 + 4) / 4.0 * leftDistanceError
            + QuadrantSum(8 * 4 + 4) / 4.0 * bottomDistanceError;

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            new(
                (float)(area[3] - area[2]),
                (float)(area[2] - area[1]),
                (float)(area[1] - area[0]),
                (float)area[0]
            ),
            new(
                (float)(area[4] - area[3]),
                (float)(area[5] - area[4]),
                (float)(area[6] - area[5]),
                (float)(area[7] - area[6])
            ),
            VectorAt(8 * 3 + 0),
            VectorAt(8 * 2 + 0),
            VectorAt(8 * 1 + 0),
            VectorAt(8 * 0 + 0),
            VectorAt(8 * 4 + 0),
            VectorAt(8 * 5 + 0),
            VectorAt(8 * 6 + 0),
            VectorAt(8 * 7 + 0),
            ReverseVectorAt(8 * 3 + 4),
            ReverseVectorAt(8 * 2 + 4),
            ReverseVectorAt(8 * 1 + 4),
            ReverseVectorAt(8 * 0 + 4),
            ReverseVectorAt(8 * 4 + 4),
            ReverseVectorAt(8 * 5 + 4),
            ReverseVectorAt(8 * 6 + 4),
            ReverseVectorAt(8 * 7 + 4)
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

            Parallel.For(
                0,
                width,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (i) =>
                {
                    int x = i + _lightMapArea.X;
                    int y = _lightMapArea.Y;
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
        Vector3[] workingLightMap, Vec4[] workingLights, int index, Vector3[] colors, int width, int height
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
                    Vec4 verticalLight
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
                        Vec4 horizontalLight = workingLights[y];

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
                            Vec4.Dot(verticalLight, spread.LightFromBottom)
                            + Vec4.Dot(horizontalLight, spread.LightFromLeft)
                        );
                        workingLights[y]
                            = (
                                (
                                    (
                                        horizontalLight.X * spread.RightFromLeftX
                                        + horizontalLight.Y * spread.RightFromLeftY
                                    )
                                    + (
                                        horizontalLight.Z * spread.RightFromLeftZ
                                        + horizontalLight.W * spread.RightFromLeftW
                                    )
                                )
                                + (
                                    (
                                        verticalLight.X * spread.RightFromBottomX
                                        + verticalLight.Y * spread.RightFromBottomY
                                    )
                                    + (
                                        verticalLight.Z * spread.RightFromBottomZ
                                        + verticalLight.W * spread.RightFromBottomW
                                    )
                                )
                            ) * mask[spread.DistanceToRight];
                        verticalLight
                            = (
                                (
                                    (
                                        horizontalLight.X * spread.TopFromLeftX
                                        + horizontalLight.Y * spread.TopFromLeftY
                                    )
                                    + (
                                        horizontalLight.Z * spread.TopFromLeftZ
                                        + horizontalLight.W * spread.TopFromLeftW
                                    )
                                )
                                + (
                                    (
                                        verticalLight.X * spread.TopFromBottomX
                                        + verticalLight.Y * spread.TopFromBottomY
                                    )
                                    + (
                                        verticalLight.Z * spread.TopFromBottomZ
                                        + verticalLight.W * spread.TopFromBottomW
                                    )
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
                + (doUpperRight ? baseWork * baseWork : 0)
                + (doUpperLeft ? baseWork * baseWork : 0)
                + (doLowerRight ? baseWork * baseWork : 0)
                + (doLowerLeft ? baseWork * baseWork : 0);

            Interlocked.Add(ref _temporalData, approximateWorkDone);
        }
    }
}
