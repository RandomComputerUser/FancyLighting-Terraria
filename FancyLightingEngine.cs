using FancyLighting.Config;
using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;

namespace FancyLighting;

internal readonly record struct LightingSpread(
    float LightFromLeft,
    float TopLightFromLeft,
    float TopLightFromBottom,
    int DistanceToTop,
    float LightFromBottom,
    float RightLightFromBottom,
    float RightLightFromLeft,
    int DistanceToRight
);

internal readonly record struct DistanceCache(double top, double right);

internal sealed class FancyLightingEngine
{
    private const int MAX_LIGHT_RANGE = 64;
    private const int DISTANCE_TICKS = 256;
    private const int MAX_DISTANCE = 384;
    private readonly float[] _lightAirDecay;
    private readonly float[] _lightSolidDecay;
    private readonly float[] _lightWaterDecay;
    private readonly float[] _lightHoneyDecay;
    private readonly float[] _lightShadowPaintDecay; // In vanilla shadow paint isn't a special case
    private float _brightnessCutoff;
    private float _logSlowestDecay;

    private float _lightLossExitingSolid;

    private const float LOW_LIGHT_LEVEL = 0.03f;
    private const float GLOBAL_ILLUMINATION_MULT = 0.55f;

    private readonly LightingSpread[] _precomputedLightingSpread;
    private readonly ThreadLocal<float[]> _workingLights = new(() => new float[MAX_LIGHT_RANGE + 1]);
    private readonly int[][] _circles;

    private Vector3[] _tmp;
    private Vector3[] _tmp2;
    private float[][] _lightMask;

    internal Rectangle _lightMapArea;

    private int _temporalData;

    public FancyLightingEngine()
    {
        ComputeLightingSpread(ref _precomputedLightingSpread);

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

        _circles = new int[MAX_LIGHT_RANGE + 1][];
        _circles[0] = new int[] { 0 };
        for (int radius = 1; radius < MAX_LIGHT_RANGE + 1; ++radius)
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

        _temporalData = 0;
    }

    private void ComputeLightingSpread(ref LightingSpread[] values)
    {
        double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        int DoubleToIndex(double x) => Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, MAX_DISTANCE);

        double Integrate(Func<double, double> fun, double lowerBound, double upperBound, int steps = 64)
        {
            // Simpson's rule

            if (steps <= 0)
            {
                steps = 64;
            }

            double sum = fun(lowerBound);

            double middleOffset = (upperBound - lowerBound) * 0.5 / steps;
            for (int i = 1; i < steps; ++i)
            {
                double t = (double)i / steps;
                double x = (1 - t) * lowerBound + t * upperBound;
                sum += 4.0 * fun(x - middleOffset) + 2.0 * fun(x);
            }

            sum += 4.0 * fun(upperBound - middleOffset) + fun(upperBound);
            return (upperBound - lowerBound) * sum / (6.0 * steps);
        }

        void CalculateLeftStats(
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
                adjacentDistance = Hypot(i, 1.0) - i;
                return;
            }
            // i should never be 0

            adjacentDistance = Hypot(i, j + 1) - Hypot(i, j);

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

            double lightFromLeft = Integrate(
                (double angle) =>
                {
                    double tanValue = Math.Tan(angle);

                    double x = i + 0.5;
                    double y = x * tanValue;
                    if (y > j + 0.5)
                    {
                        y = j + 0.5;
                        x = y / tanValue;
                    }
                    return Hypot(x - (i - 0.5), y - (i - 0.5) * tanValue);
                },
                bottomLeftAngle,
                topLeftAngle);
            double lightFromBottom = Integrate(
                (double angle) =>
                {
                    double tanValue = Math.Tan(angle);

                    double x = i + 0.5;
                    double y = x * tanValue;
                    if (y > j + 0.5)
                    {
                        y = j + 0.5;
                        x = y / tanValue;
                    }
                    return Hypot(x - (j - 0.5) / tanValue, y - (j - 0.5));
                },
                bottomRightAngle,
                bottomLeftAngle);

            spread = lightFromLeft / (lightFromLeft + lightFromBottom);
        }

        values = new LightingSpread[(MAX_LIGHT_RANGE + 1) * (MAX_LIGHT_RANGE + 1)];
        DistanceCache[,] distances = new DistanceCache[MAX_LIGHT_RANGE + 1, MAX_LIGHT_RANGE + 1];

        for (int i = 1; i < MAX_LIGHT_RANGE + 1; ++i)
        {
            CalculateLeftStats(i, 0,
                out double lightFromLeft, out double topLightFromLeft,
                out double distanceToTop);
            values[(MAX_LIGHT_RANGE + 1) * i] = new LightingSpread(
                (float)lightFromLeft,
                (float)topLightFromLeft,
                (float)(1.0 - topLightFromLeft),
                DoubleToIndex(distanceToTop),
                0f,
                0f,
                1f,
                DoubleToIndex(1.0)
            );
            distances[i, 0] = new DistanceCache((double)DISTANCE_TICKS * i + DoubleToIndex(distanceToTop), (double)DISTANCE_TICKS * (i + 1));
        }

        for (int j = 1; j < MAX_LIGHT_RANGE + 1; ++j)
        {
            CalculateLeftStats(j, 0,
                out double lightFromBottom, out double rightLightFromBottom,
                out double distanceToRight);
            values[j] = new LightingSpread(
                0f,
                0f,
                1f,
                DoubleToIndex(1.0),
                (float)lightFromBottom,
                (float)rightLightFromBottom,
                (float)(1.0 - rightLightFromBottom),
                DoubleToIndex(distanceToRight)
            );
            distances[0, j] = new DistanceCache((double)DISTANCE_TICKS * (j + 1), (double)DISTANCE_TICKS * j + DoubleToIndex(distanceToRight));
        }

        for (int j = 1; j < MAX_LIGHT_RANGE + 1; ++j)
        {
            for (int i = 1; i < MAX_LIGHT_RANGE + 1; ++i)
            {
                CalculateLeftStats(
                    i, j,
                    out double lightFromLeft, out double topLightFromLeft,
                    out double distanceToTop);
                CalculateLeftStats(j, i,
                    out double lightFromBottom, out double rightLightFromBottom,
                    out double distanceToRight);

                double leftError = distances[i - 1, j].right / DISTANCE_TICKS - Hypot(i, j);
                double bottomError = distances[i, j - 1].top / DISTANCE_TICKS - Hypot(i, j);
                distanceToTop -= topLightFromLeft * leftError + (1.0 - topLightFromLeft) * bottomError;
                distanceToRight -= rightLightFromBottom * bottomError + (1.0 - rightLightFromBottom) * leftError;

                distances[i, j] = new DistanceCache(
                    topLightFromLeft * (DoubleToIndex(distanceToTop) + distances[i - 1, j].right)
                        + (1.0 - topLightFromLeft) * (DoubleToIndex(distanceToTop) + distances[i, j - 1].top),
                    rightLightFromBottom * (DoubleToIndex(distanceToRight) + distances[i, j - 1].top)
                        + (1.0 - rightLightFromBottom) * (DoubleToIndex(distanceToRight) + distances[i - 1, j].right)
                );

                values[(MAX_LIGHT_RANGE + 1) * i + j] = new LightingSpread(
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

    internal void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightDecay,
        int width,
        int height
    )
    {
        _lightLossExitingSolid = LightingConfig.Instance.FancyLightingEngineExitMultiplier();

        double temporalMult = LightingConfig.Instance.SimulateGlobalIllumination ? 0.25 : 1.0;
        _brightnessCutoff = LightingConfig.Instance.FancyLightingEngineUseTemporal
            ? (float)Math.Clamp(Math.Sqrt(_temporalData / 55555.5 * temporalMult) * 0.02, 0.02, 0.125)
            : 0.04f;
        _temporalData = 0;

        const float MAX_DECAY_VALUE = 0.97f;

        float decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
        float lightAirDecayBaseline
            = decayMult * Math.Min(lightMap.LightDecayThroughAir, MAX_DECAY_VALUE);
        float lightSolidDecayBaseline = decayMult * Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            MAX_DECAY_VALUE
        );
        float lightWaterDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughWater.X,
                Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
            ),
            MAX_DECAY_VALUE
        );
        float lightHoneyDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughHoney.X,
                Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
            ),
            MAX_DECAY_VALUE
        );

        _logSlowestDecay = MathF.Log(
            Math.Max(
                Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
                Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
            )
        );

        const float exponentMult = 1f / DISTANCE_TICKS;

        if (lightAirDecayBaseline != _lightAirDecay[DISTANCE_TICKS])
        {
            float logBaseline = MathF.Log(lightAirDecayBaseline);
            for (int exponent = 0; exponent <= MAX_DISTANCE; ++exponent)
            {
                _lightAirDecay[exponent] = MathF.Exp(exponentMult * exponent * logBaseline);
            }
            _lightAirDecay[DISTANCE_TICKS] = lightAirDecayBaseline;
        }
        if (lightSolidDecayBaseline != _lightSolidDecay[DISTANCE_TICKS])
        {
            float logBaseline = MathF.Log(lightSolidDecayBaseline);
            for (int exponent = 0; exponent <= MAX_DISTANCE; ++exponent)
            {
                _lightSolidDecay[exponent] = MathF.Exp(exponentMult * exponent * logBaseline);
            }
            _lightSolidDecay[DISTANCE_TICKS] = lightSolidDecayBaseline;
        }
        if (lightWaterDecayBaseline != _lightWaterDecay[DISTANCE_TICKS])
        {
            float logBaseline = MathF.Log(lightWaterDecayBaseline);
            for (int exponent = 0; exponent <= MAX_DISTANCE; ++exponent)
            {
                _lightWaterDecay[exponent] = MathF.Exp(exponentMult * exponent * logBaseline);
            }
            _lightWaterDecay[DISTANCE_TICKS] = lightWaterDecayBaseline;
        }
        if (lightHoneyDecayBaseline != _lightHoneyDecay[DISTANCE_TICKS])
        {
            float logBaseline = MathF.Log(lightHoneyDecayBaseline);
            for (int exponent = 0; exponent <= MAX_DISTANCE; ++exponent)
            {
                _lightHoneyDecay[exponent] = MathF.Exp(exponentMult * exponent * logBaseline);
            }
            _lightHoneyDecay[DISTANCE_TICKS] = lightHoneyDecayBaseline;
        }

        int length = width * height;

        if (_tmp is null || _tmp.Length < length)
        {
            _tmp = new Vector3[length];
            _lightMask = new float[length][];
        }

        Array.Copy(colors, _tmp, length);

        Parallel.For(
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
                    _lightMask[j] = lightDecay[j] switch
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

            Parallel.For(
                0,
                width,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (i) =>
                {
                    int endIndex = height * (i + 1);
                    for (int j = height * i; j < endIndex; ++j)
                    {
                        Vector3.Multiply(ref _tmp[j], GLOBAL_ILLUMINATION_MULT, out _tmp2[j]);
                    }
                }
            );

            Parallel.For(
                0,
                length,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (i) =>
                {
                    if (_lightMask[i] == _lightSolidDecay || _lightMask[i] == _lightShadowPaintDecay)
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

        float threshold = _lightMask[index][MAX_DISTANCE];
        int length = width * height;

        int topEdge = height * (index / height);
        int bottomEdge = topEdge + (height - 1);

        bool skipUp, skipDown, skipLeft, skipRight;

        if (index == topEdge)
        {
            skipUp = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index - 1][DISTANCE_TICKS] / threshold * colors[index - 1];
            skipUp = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index == bottomEdge)
        {
            skipDown = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index + 1][DISTANCE_TICKS] / threshold * colors[index + 1];
            skipDown = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index < height)
        {
            skipLeft = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index - height][DISTANCE_TICKS] / threshold * colors[index - height];
            skipLeft = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
        }
        if (index + height >= length)
        {
            skipRight = true;
        }
        else
        {
            Vector3 otherColor = _lightMask[index + height][DISTANCE_TICKS] / threshold * colors[index + height];
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
                MathF.Log(
                    _brightnessCutoff / (initialDecay * Math.Max(color.X, Math.Max(color.Y, color.Z)))
                ) / _logSlowestDecay
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

        int midX = index / height;

        int leftEdge = Math.Min(midX, lightRange);
        int rightEdge = Math.Min(width - 1 - midX, lightRange);
        topEdge = Math.Min(index - topEdge, lightRange);
        bottomEdge = Math.Min(bottomEdge - index, lightRange);

        int[] circle = _circles[lightRange];

        // precomputedLightingSpread[,]: 2D arrays in C# are row major (with the first index being the row)
        // precomputedLightingSpread uses y as the second index,
        // and Terraria's 1D arrays for lighting use height * x + y as the index
        // So looping over y in the inner loop should be faster and simpler

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            float[] workingLights = _workingLights.Value;

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

                    workingLights[y] = value * mask[_precomputedLightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= rightEdge; ++x)
                {
                    int i = index + height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
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
                            = ref _precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
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

                    workingLights[y] = value * mask[_precomputedLightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= leftEdge; ++x)
                {
                    int i = index - height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
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
                            = ref _precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
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

                    workingLights[y] = value * mask[_precomputedLightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= rightEdge; ++x)
                {
                    int i = index + height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
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
                            = ref _precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
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

                    workingLights[y] = value * mask[_precomputedLightingSpread[y].DistanceToRight];
                }
                for (int x = 1; x <= leftEdge; ++x)
                {
                    int i = index - height * x;
                    float[] mask = _lightMask[i];

                    float verticalLight
                        = workingLights[0]
                        * mask[_precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x].DistanceToTop];
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
                            = ref _precomputedLightingSpread[(MAX_LIGHT_RANGE + 1) * x + y];
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

        if (LightingConfig.Instance.FancyLightingEngineUseTemporal)
        {
            int baseWork = Math.Clamp(
                (int)Math.Ceiling(
                    MathF.Log(
                        0.04f / (initialDecay * Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) / _logSlowestDecay
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
