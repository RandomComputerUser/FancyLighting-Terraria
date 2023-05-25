using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;

namespace FancyLighting.LightingEngines;

internal sealed class EnhancedFancyLightingEngine : FancyLightingEngineBase
{
    private readonly record struct LightingSpread(
        float LightFromLeft,
        float LightFromBottom,
        float TopLightFromLeft,
        float TopLightFromBottom,
        int LeftToTopDistance,
        int BottomToTopDistance,
        float RightLightFromLeft,
        float RightLightFromBottom,
        int LeftToRightDistance,
        int BottomToRightDistance
    );

    private const int MAX_LIGHT_RANGE = 64;
    private const int DISTANCE_TICKS = 256;
    private const int MAX_DISTANCE = 384;
    private float _logBrightnessCutoff;
    private float _reciprocalLogSlowestDecay;

    private float _lightLossExitingSolid;

    private const float LOW_LIGHT_LEVEL = 0.03f;
    private const float GI_MULT_BASE = 0.45f;
    private const float GI_MULT_BACKGROUND = 0.55f;
    private const float GI_MULT_FOREGROUND = 0.65f;

    private readonly LightingSpread[] _lightingSpread;
    private readonly ThreadLocal<float[]> _workingLights = new(() => new float[MAX_LIGHT_RANGE + 1]);

    private Vector3[] _tmp;
    private Vector3[] _tmp2;

    private int _temporalData;

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

        for (int row = 0; row <= MAX_LIGHT_RANGE; ++row)
        {
            for (int col = 0; col <= MAX_LIGHT_RANGE; ++col)
            {
                values[(MAX_LIGHT_RANGE + 1) * col + row] = CalculateTileLightingSpread(row, col);
            }
        }
    }

    private static LightingSpread CalculateTileLightingSpread(int row, int col)
    {
        static int DoubleToIndex(double x)
            => Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, MAX_DISTANCE);

        if (row == 0 && col == 0)
        {
            return new(0f, 0f, 0f, 0f, 0, 0, 0f, 0f, 0, 0);
        }

        if (row == 0)
        {
            return new(
                1f,
                0f,
                1f,
                0f,
                DoubleToIndex(MathUtil.Hypot(col, 1) - col),
                0,
                1f,
                0f,
                DISTANCE_TICKS,
                0
            );
        }

        if (col == 0)
        {
            return new(
                0f,
                1f,
                0f,
                1f,
                0,
                DISTANCE_TICKS,
                0f,
                1f,
                0,
                DoubleToIndex(MathUtil.Hypot(1, row) - row)
            );
        }

        double topLeftAngle = Math.Atan2(row + 0.5, col - 0.5);
        double topRightAngle = Math.Atan2(row + 0.5, col + 0.5);
        double bottomLeftAngle = Math.Atan2(row - 0.5, col - 0.5);
        double bottomRightAngle = Math.Atan2(row - 0.5, col + 0.5);

        double leftToRight = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);
                double x = col + 0.5;
                double y = x * tanValue;
                return MathUtil.Hypot(x - (col - 0.5), y - (col - 0.5) * tanValue);
            },
            bottomLeftAngle,
            Math.Max(bottomLeftAngle, topRightAngle)
        );
        double leftToTop = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);
                double y = row + 0.5;
                double x = y / tanValue;
                return MathUtil.Hypot(x - (col - 0.5), y - (col - 0.5) * tanValue);
            },
            Math.Max(bottomLeftAngle, topRightAngle),
            topLeftAngle
        );
        double bottomToTop = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);
                double y = row + 0.5;
                double x = y / tanValue;
                return MathUtil.Hypot(x - (row - 0.5) / tanValue, y - (row - 0.5));
            },
            Math.Min(bottomLeftAngle, topRightAngle),
            bottomLeftAngle
        );
        double bottomToRight = MathUtil.Integrate(
            (angle) =>
            {
                double tanValue = Math.Tan(angle);
                double x = col + 0.5;
                double y = x * tanValue;
                return MathUtil.Hypot(x - (row - 0.5) / tanValue, y - (row - 0.5));
            },
            bottomRightAngle,
            Math.Min(bottomLeftAngle, topRightAngle)
        );

        double totalLight = leftToRight + leftToTop + bottomToTop + bottomToRight;

        double lightFromLeft;
        double lightFromBottom;
        if (row == col)
        {
            lightFromLeft = 0.5;
            lightFromBottom = 0.5;
        }
        else if (row < col)
        {
            lightFromLeft = (leftToRight + leftToTop) / totalLight;
            lightFromBottom = 1.0 - lightFromLeft;
        }
        else
        {
            lightFromBottom = (bottomToTop + bottomToRight) / totalLight;
            lightFromLeft = 1.0 - lightFromBottom;
        }

        leftToTop /= topLeftAngle - Math.Max(bottomLeftAngle, topRightAngle);
        if (topRightAngle > bottomLeftAngle)
        {
            leftToRight /= topRightAngle - bottomLeftAngle;
        }
        bottomToRight /= Math.Min(bottomLeftAngle, topRightAngle) - bottomRightAngle;
        if (topRightAngle < bottomLeftAngle)
        {
            bottomToTop /= bottomLeftAngle - topRightAngle;
        }

        double topFromLeft;
        double topFromBottom;
        if (bottomLeftAngle > topRightAngle)
        {
            topFromBottom = (bottomLeftAngle - topRightAngle) / (topLeftAngle - topRightAngle);
            topFromLeft = 1.0 - topFromBottom;
        }
        else
        {
            topFromLeft = 1.0;
            topFromBottom = 0.0;
        }

        double rightFromBottom;
        double rightFromLeft;
        if (bottomLeftAngle < topRightAngle)
        {
            rightFromLeft = (bottomLeftAngle - topRightAngle) / (bottomRightAngle - topRightAngle);
            rightFromBottom = 1.0 - rightFromLeft;
        }
        else
        {
            rightFromBottom = 1.0;
            rightFromLeft = 0.0;
        }

        return new(
            (float)lightFromLeft,
            (float)lightFromBottom,
            (float)topFromLeft,
            (float)topFromBottom,
            DoubleToIndex(leftToTop),
            DoubleToIndex(bottomToTop),
            (float)rightFromLeft,
            (float)rightFromBottom,
            DoubleToIndex(leftToRight),
            DoubleToIndex(bottomToRight)
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

        double temporalMult = LightingConfig.Instance.SimulateGlobalIllumination ? 0.25 : 1.0;
        _logBrightnessCutoff = FancyLightingMod._inCameraMode
            ? 0.02f
            : LightingConfig.Instance.FancyLightingEngineUseTemporal
                ? (float)Math.Clamp(Math.Sqrt(_temporalData / 55555.5 * temporalMult) * 0.02, 0.02, 0.125)
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

        Parallel.For(
            0,
            length,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (i) => ProcessLight(i, colors, width, height)
        );

        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            if (_tmp2 is null || _tmp2.Length < length)
            {
                _tmp2 = new Vector3[length];
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
                        ref Vector3 giLight = ref _tmp2[j];

                        if (lightMasks[j] is LightMaskMode.Solid)
                        {
                            giLight.X = 0f;
                            giLight.Y = 0f;
                            giLight.Z = 0f;
                            continue;
                        }

                        float mult;
                        if (
                            y > ymin && lightMasks[j - 1] is LightMaskMode.Solid
                            || y < ymax && lightMasks[j + 1] is LightMaskMode.Solid
                            || notOnLeft && lightMasks[j - height] is LightMaskMode.Solid
                            || notOnRight && lightMasks[j + height] is LightMaskMode.Solid
                        )
                        {
                            mult = GI_MULT_FOREGROUND;
                        }
                        else if (Main.tile[x, y].WallType != WallID.None)
                        {
                            mult = GI_MULT_BACKGROUND;
                        }
                        else
                        {
                            mult = GI_MULT_BASE;
                        }

                        Vector3.Multiply(ref _tmp[j], mult, out giLight);
                    }
                }
            );

            Parallel.For(
                0,
                length,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (i) =>
                {
                    if (lightMasks[i] is LightMaskMode.Solid)
                    {
                        return;
                    }

                    ProcessLight(i, _tmp2, width, height);
                }
            );
        }

        Array.Copy(_tmp, colors, length);
    }

    private void ProcessLight(int index, Vector3[] colors, int width, int height)
    {
        Vector3 color = colors[index];
        if (color.X <= LOW_LIGHT_LEVEL && color.Y <= LOW_LIGHT_LEVEL && color.Z <= LOW_LIGHT_LEVEL)
        {
            return;
        }

        void SetLightMap(int i, float value)
        {
            ref Vector3 light = ref _tmp[i];
            Vector3.Multiply(ref color, value, out Vector3 newLight);
            float oldValue;
            do
            {
                oldValue = light.X;
                if (oldValue >= newLight.X)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref light.X, newLight.X, oldValue) != oldValue);
            do
            {
                oldValue = light.Y;
                if (oldValue >= newLight.Y)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref light.Y, newLight.Y, oldValue) != oldValue);
            do
            {
                oldValue = light.Z;
                if (oldValue >= newLight.Z)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref light.Z, newLight.Z, oldValue) != oldValue);
        }

        float reciprocalThreshold = 1f / _lightMask[index][MAX_DISTANCE];
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
            float[] workingLights = _workingLights.Value;

            void ProcessQuadrant(int edgeY, int edgeX, int indexVerticalChange, int indexHorizontalChange)
            {
                workingLights[0] = initialDecay;
                float value = 1f;
                for (int i = index, y = 1; y <= edgeY; ++y)
                {
                    value *= _lightMask[i][DISTANCE_TICKS];
                    float[] mask = _lightMask[i += indexVerticalChange];

                    if (y > 1 && mask == _lightAirDecay && _lightMask[i - indexVerticalChange] == _lightSolidDecay)
                    {
                        value *= _lightLossExitingSolid;
                    }

                    workingLights[y] = value * mask[_lightingSpread[y].BottomToRightDistance];
                }
                for (int x = 1; x <= edgeX; ++x)
                {
                    int i = index + indexHorizontalChange * x;
                    float[] mask = _lightMask[i];

                    int j = (MAX_LIGHT_RANGE + 1) * x;
                    float verticalLight
                        = workingLights[0]
                        * mask[_lightingSpread[j].LeftToTopDistance];
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
                        float horizontalLight = workingLights[y];

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
                            spread.LightFromBottom * verticalLight
                            + spread.LightFromLeft * horizontalLight
                        );
                        workingLights[y]
                            = spread.RightLightFromLeft * mask[spread.LeftToRightDistance] * horizontalLight
                            + spread.RightLightFromBottom * mask[spread.BottomToRightDistance] * verticalLight;
                        verticalLight
                            = spread.TopLightFromLeft * mask[spread.LeftToTopDistance] * horizontalLight
                            + spread.TopLightFromBottom * mask[spread.BottomToTopDistance] * verticalLight;
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

        if (LightingConfig.Instance.FancyLightingEngineUseTemporal)
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
