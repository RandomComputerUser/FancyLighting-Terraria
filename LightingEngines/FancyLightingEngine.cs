using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria.Graphics.Light;

namespace FancyLighting.LightingEngines;

internal sealed class FancyLightingEngine : FancyLightingEngineBase<float>
{
    private readonly record struct LightingSpread(
        float LightFromLeft,
        float TopLightFromLeft,
        float TopLightFromBottom,
        int DistanceToTop,
        float LightFromBottom,
        float RightLightFromBottom,
        float RightLightFromLeft,
        int DistanceToRight
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

    public FancyLightingEngine()
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
        static int DoubleToIndex(double x)
            => Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, MAX_DISTANCE);

        values = new LightingSpread[(MAX_LIGHT_RANGE + 1) * (MAX_LIGHT_RANGE + 1)];
        DistanceCache[,] distances = new DistanceCache[MAX_LIGHT_RANGE + 1, MAX_LIGHT_RANGE + 1];

        for (int i = 1; i < MAX_LIGHT_RANGE + 1; ++i)
        {
            CalculateLeftStats(i, 0,
                out double lightFromLeft, out double topLightFromLeft,
                out double distanceToTop
            );
            values[(MAX_LIGHT_RANGE + 1) * i] = new(
                (float)lightFromLeft,
                (float)topLightFromLeft,
                (float)(1.0 - topLightFromLeft),
                DoubleToIndex(distanceToTop),
                0f,
                0f,
                1f,
                DoubleToIndex(1.0)
            );
            distances[i, 0] = new DistanceCache(
                (double)DISTANCE_TICKS * i + DoubleToIndex(distanceToTop), (double)DISTANCE_TICKS * (i + 1)
            );
        }

        for (int j = 1; j < MAX_LIGHT_RANGE + 1; ++j)
        {
            CalculateLeftStats(j, 0,
                out double lightFromBottom, out double rightLightFromBottom,
                out double distanceToRight
            );
            values[j] = new(
                0f,
                0f,
                1f,
                DoubleToIndex(1.0),
                (float)lightFromBottom,
                (float)rightLightFromBottom,
                (float)(1.0 - rightLightFromBottom),
                DoubleToIndex(distanceToRight)
            );
            distances[0, j] = new(
                (double)DISTANCE_TICKS * (j + 1), (double)DISTANCE_TICKS * j + DoubleToIndex(distanceToRight)
            );
        }

        for (int j = 1; j < MAX_LIGHT_RANGE + 1; ++j)
        {
            for (int i = 1; i < MAX_LIGHT_RANGE + 1; ++i)
            {
                CalculateLeftStats(
                    i, j,
                    out double lightFromLeft, out double topLightFromLeft,
                    out double distanceToTop
                );
                CalculateLeftStats(j, i,
                    out double lightFromBottom, out double rightLightFromBottom,
                    out double distanceToRight
                );

                double leftError = distances[i - 1, j].Right / DISTANCE_TICKS - MathUtil.Hypot(i, j);
                double bottomError = distances[i, j - 1].Top / DISTANCE_TICKS - MathUtil.Hypot(i, j);
                distanceToTop -= topLightFromLeft * leftError + (1.0 - topLightFromLeft) * bottomError;
                distanceToRight -= rightLightFromBottom * bottomError + (1.0 - rightLightFromBottom) * leftError;

                distances[i, j] = new DistanceCache(
                    topLightFromLeft * (DoubleToIndex(distanceToTop) + distances[i - 1, j].Right)
                        + (1.0 - topLightFromLeft) * (DoubleToIndex(distanceToTop) + distances[i, j - 1].Top),
                    rightLightFromBottom * (DoubleToIndex(distanceToRight) + distances[i, j - 1].Top)
                        + (1.0 - rightLightFromBottom) * (DoubleToIndex(distanceToRight) + distances[i - 1, j].Right)
                );

                values[(MAX_LIGHT_RANGE + 1) * i + j] = new(
                    (float)lightFromLeft,
                    (float)topLightFromLeft,
                    (float)(1.0 - topLightFromLeft),
                    DoubleToIndex(distanceToTop),
                    (float)lightFromBottom,
                    (float)rightLightFromBottom,
                    (float)(1.0 - rightLightFromBottom),
                    DoubleToIndex(distanceToRight)
                );
            }
        }
    }

    private static void CalculateLeftStats(
        int i,
        int j,
        out double spread,
        out double adjacentFrom,
        out double adjacentDistance
    )
    {
        if (j == 0)
        {
            spread = 1.0;
            adjacentFrom = 1.0;
            adjacentDistance = MathUtil.Hypot(i, 1.0) - i;
            return;
        }
        // i should never be 0

        adjacentDistance = MathUtil.Hypot(i, j + 1) - MathUtil.Hypot(i, j);

        double slope = (j - 0.5) / (i - 0.5);
        double bottomLeftAngle = Math.Atan2(j - 0.5, i - 0.5);
        double topRightAngle = Math.Atan2(j + 0.5, i + 0.5);
        double topLeftAngle = Math.Atan2(j + 0.5, i - 0.5);
        double bottomRightAngle = Math.Atan2(j - 0.5, i + 0.5);

        adjacentFrom = slope <= 1.0
            ? 1.0
            : (topLeftAngle - Math.Atan2(j + 0.5, i - 0.5 + 1.0 / slope)) / (topLeftAngle - topRightAngle);

        if (slope == 1.0)
        {
            spread = 0.5;
            return;
        }

        double lightFromLeft = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);

                double x = i + 0.5;
                double y = x * tanValue;
                if (y > j + 0.5)
                {
                    y = j + 0.5;
                    x = y / tanValue;
                }
                return MathUtil.Hypot(x - (i - 0.5), y - (i - 0.5) * tanValue);
            },
            bottomLeftAngle,
            topLeftAngle
        );
        double lightFromBottom = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);

                double x = i + 0.5;
                double y = x * tanValue;
                if (y > j + 0.5)
                {
                    y = j + 0.5;
                    x = y / tanValue;
                }
                return MathUtil.Hypot(x - (j - 0.5) / tanValue, y - (j - 0.5));
            },
            bottomRightAngle,
            bottomLeftAngle
        );

        spread = lightFromLeft / (lightFromLeft + lightFromBottom);
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

        if (doGI)
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
                    int endIndex = height * (i + 1);
                    for (int j = height * i; j < endIndex; ++j)
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
        Vector3[] workingLightMap, float[] workingLights, int index, Vector3[] colors, int width, int height
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

        // Performance optimization
        float[][] _lightMask = this._lightMask;

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

        // Performance optimization
        float[] _lightAirDecay = this._lightAirDecay;
        float[] _lightSolidDecay = this._lightSolidDecay;
        float _lightLossExitingSolid = this._lightLossExitingSolid;

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
            // Performance optimization
            LightingSpread[] _lightingSpread = this._lightingSpread;

            // Upper Right
            if (doUpperRight)
            {
                workingLights[0] = initialDecay;
                float value = 1f;
                for (int i = index, y = 1; y <= topEdge; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[--i];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = value * mask[_lightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= rightEdge; ++x)
                {
                    int i = index + height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
                    workingLights[0] *= mask[DISTANCE_TICKS];
                    if (x > 1 && mask == _lightAirDecay && _lightMask[i - height] == _lightSolidDecay)
                    {
                        verticalLight *= _lightLossExitingSolid;
                        workingLights[0] *= _lightLossExitingSolid;
                    }

                    int edge = Math.Min(topEdge, circle[x]);
                    for (int y = 1; y <= edge; ++y)
                    {
                        mask = _lightMask[--i];
                        float horizontalLight = workingLights[y];

                        if (mask == _lightAirDecay)
                        {
                            if (_lightMask[i + 1] == _lightSolidDecay)
                            {
                                verticalLight *= _lightLossExitingSolid;
                            }

                            if (_lightMask[i - height] == _lightSolidDecay)
                            {
                                horizontalLight *= _lightLossExitingSolid;
                            }
                        }
                        ref LightingSpread spread
                            = ref _lightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
                        SetLightMap(i,
                            spread.LightFromBottom * verticalLight
                            + spread.LightFromLeft * horizontalLight
                        );
                        workingLights[y]
                            = (spread.RightLightFromBottom * verticalLight + spread.RightLightFromLeft * horizontalLight)
                            * mask[spread.DistanceToRight];
                        verticalLight
                            = (spread.TopLightFromLeft * horizontalLight + spread.TopLightFromBottom * verticalLight)
                            * mask[spread.DistanceToTop];
                    }
                }
            }

            // Upper Left
            if (doUpperLeft)
            {
                workingLights[0] = initialDecay;
                float value = 1f;
                for (int i = index, y = 1; y <= topEdge; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[--i];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = value * mask[_lightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= leftEdge; ++x)
                {
                    int i = index - height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
                    workingLights[0] *= mask[DISTANCE_TICKS];
                    if (x > 1 && mask == _lightAirDecay && _lightMask[i + height] == _lightSolidDecay)
                    {
                        verticalLight *= _lightLossExitingSolid;
                        workingLights[0] *= _lightLossExitingSolid;
                    }

                    int edge = Math.Min(topEdge, circle[x]);
                    for (int y = 1; y <= edge; ++y)
                    {
                        mask = _lightMask[--i];
                        float horizontalLight = workingLights[y];

                        if (mask == _lightAirDecay)
                        {
                            if (_lightMask[i + 1] == _lightSolidDecay)
                            {
                                verticalLight *= _lightLossExitingSolid;
                            }

                            if (_lightMask[i + height] == _lightSolidDecay)
                            {
                                horizontalLight *= _lightLossExitingSolid;
                            }
                        }
                        ref LightingSpread spread
                            = ref _lightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
                        SetLightMap(i,
                            spread.LightFromBottom * verticalLight
                            + spread.LightFromLeft * horizontalLight
                        );
                        workingLights[y]
                            = (spread.RightLightFromBottom * verticalLight + spread.RightLightFromLeft * horizontalLight)
                            * mask[spread.DistanceToRight];
                        verticalLight
                            = (spread.TopLightFromLeft * horizontalLight + spread.TopLightFromBottom * verticalLight)
                            * mask[spread.DistanceToTop];
                    }
                }
            }

            // Lower Right
            if (doLowerRight)
            {
                workingLights[0] = initialDecay;
                float value = 1f;
                for (int i = index, y = 1; y <= bottomEdge; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[++i];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = value * mask[_lightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= rightEdge; ++x)
                {
                    int i = index + height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
                    workingLights[0] *= mask[DISTANCE_TICKS];
                    if (x > 1 && mask == _lightAirDecay && _lightMask[i - height] == _lightSolidDecay)
                    {
                        verticalLight *= _lightLossExitingSolid;
                        workingLights[0] *= _lightLossExitingSolid;
                    }

                    int edge = Math.Min(bottomEdge, circle[x]);
                    for (int y = 1; y <= edge; ++y)
                    {
                        mask = _lightMask[++i];
                        float horizontalLight = workingLights[y];

                        if (mask == _lightAirDecay)
                        {
                            if (_lightMask[i - 1] == _lightSolidDecay)
                            {
                                verticalLight *= _lightLossExitingSolid;
                            }

                            if (_lightMask[i - height] == _lightSolidDecay)
                            {
                                horizontalLight *= _lightLossExitingSolid;
                            }
                        }
                        ref LightingSpread spread
                            = ref _lightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
                        SetLightMap(i,
                            spread.LightFromBottom * verticalLight
                            + spread.LightFromLeft * horizontalLight
                        );
                        workingLights[y]
                            = (spread.RightLightFromBottom * verticalLight + spread.RightLightFromLeft * horizontalLight)
                            * mask[spread.DistanceToRight];
                        verticalLight
                            = (spread.TopLightFromLeft * horizontalLight + spread.TopLightFromBottom * verticalLight)
                            * mask[spread.DistanceToTop];
                    }
                }
            }

            // Lower Left
            if (doLowerLeft)
            {
                workingLights[0] = initialDecay;
                float value = 1f;
                for (int i = index, y = 1; y <= bottomEdge; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[++i];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = value * mask[_lightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= leftEdge; ++x)
                {
                    int i = index - height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
                    workingLights[0] *= mask[DISTANCE_TICKS];
                    if (x > 1 && mask == _lightAirDecay && _lightMask[i + height] == _lightSolidDecay)
                    {
                        verticalLight *= _lightLossExitingSolid;
                        workingLights[0] *= _lightLossExitingSolid;
                    }

                    int edge = Math.Min(bottomEdge, circle[x]);
                    for (int y = 1; y <= edge; ++y)
                    {
                        mask = _lightMask[++i];
                        float horizontalLight = workingLights[y];

                        if (mask == _lightAirDecay)
                        {
                            if (_lightMask[i - 1] == _lightSolidDecay)
                            {
                                verticalLight *= _lightLossExitingSolid;
                            }

                            if (_lightMask[i + height] == _lightSolidDecay)
                            {
                                horizontalLight *= _lightLossExitingSolid;
                            }
                        }
                        ref LightingSpread spread
                            = ref _lightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
                        SetLightMap(i,
                            spread.LightFromBottom * verticalLight
                            + spread.LightFromLeft * horizontalLight
                        );
                        workingLights[y]
                            = (spread.RightLightFromBottom * verticalLight + spread.RightLightFromLeft * horizontalLight)
                            * mask[spread.DistanceToRight];
                        verticalLight
                            = (spread.TopLightFromLeft * horizontalLight + spread.TopLightFromBottom * verticalLight)
                            * mask[spread.DistanceToTop];
                    }
                }
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
                + (
                    (!skipUp ? 1 : 0)
                    + (!skipDown ? 1 : 0)
                    + (!skipLeft ? 1 : 0)
                    + (!skipRight ? 1 : 0)
                ) * baseWork
                + (
                    (doUpperRight ? 1 : 0)
                    + (doUpperLeft ? 1 : 0)
                    + (doLowerRight ? 1 : 0)
                    + (doLowerLeft ? 1 : 0)
                ) * (baseWork * baseWork);

            Interlocked.Add(ref _temporalData, approximateWorkDone);
        }
    }
}
