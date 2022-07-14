using Microsoft.Xna.Framework;

using Terraria;
using Terraria.ID;
using Terraria.Graphics.Light;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FancyLighting
{
    internal readonly record struct LightingSpread(
        float LightFromLeft,
        float TopLightFromLeft,
        int LeftToTopDistance,
        int LeftToRightDistance,
        float LightFromBottom,
        float RightLightFromBottom,
        int BottomToRightDistance,
        int BottomToTopDistance
    );

    internal readonly record struct DistanceCache(double top, double topRounded, double right, double rightRounded);

    internal sealed class FancyLightingEngine
    {
        const int MAX_LIGHT_RANGE = 64;
        private float[] _lightAirDecay;
        private float[] _lightSolidDecay;
        private float[] _lightWaterDecay;
        private float[] _lightHoneyDecay;
        private float[] _lightShadowPaintDecay; // In vanilla shadow paint isn't a special case
        private float _brightnessCutoff;
        private double _logSlowestDecay;

        private float _lightLossExitingSolid;

        private LightingSpread[,] _precomputedLightingSpread;
        private float[][] _multithreadLightMap;
        private int[][] _circles;
        private int[] _circleAreas;

        private Vector3[] _tmp;
        private float[][] _lightMask;

        internal Rectangle _lightMapArea;

        private int _temporalData;

        public FancyLightingEngine() {
            ComputeLightingSpread(ref _precomputedLightingSpread);

            _lightAirDecay = new float[151];
            _lightSolidDecay = new float[151];
            _lightWaterDecay = new float[151];
            _lightHoneyDecay = new float[151];
            _lightShadowPaintDecay = new float[151];
            for (int exponent = 0; exponent <= 150; ++exponent)
            {
                _lightAirDecay[exponent] = 1f;
                _lightSolidDecay[exponent] = 1f;
                _lightWaterDecay[exponent] = 1f;
                _lightHoneyDecay[exponent] = 1f;
                _lightShadowPaintDecay[exponent] = (float)Math.Pow(0.175, exponent / 100.0);
            }
            _logSlowestDecay = Math.Log(0.91);

            _multithreadLightMap = new float[256][];
            for (int i = 0; i < 256; i++)
            {
                _multithreadLightMap[i] = new float[MAX_LIGHT_RANGE + 1];
            }

            _circles = new int[MAX_LIGHT_RANGE + 1][];
            _circleAreas = new int[MAX_LIGHT_RANGE + 1];
            _circles[0] = new int[] { 0 };
            _circleAreas[0] = 0;
            for (int radius = 1; radius <= MAX_LIGHT_RANGE; ++radius)
            {
                int circleArea = 0;
                _circles[radius] = new int[radius + 1];
                _circles[radius][0] = radius;
                double diagonal = radius / Math.Sqrt(2.0);
                for (int x = 1; x <= radius; ++x)
                {
                    if (x <= diagonal)
                    {
                        _circles[radius][x] = (int)Math.Ceiling(Math.Sqrt(radius * radius - x * x));
                    }
                    else
                    {
                        _circles[radius][x] = (int)Math.Floor(Math.Sqrt(radius * radius - (x - 1) * (x - 1)));
                    }
                    circleArea += _circles[radius][x];
                }
                _circleAreas[radius] = circleArea;
            }

            _temporalData = 0;
        }

        private void ComputeLightingSpread(ref LightingSpread[,] values)
        {
            double Hypot(double x, double y)
            {
                return Math.Sqrt(x * x + y * y);
            }

            int DoubleToIndex(double x)
            {
                return Math.Clamp((int)Math.Round(100.0 * x), 0, 150);
            }

            void CalculateLeftStats(int i, int j, out double spread, out double adjacentFrom, out double adjacentDecay, out double oppositeDecay)
            {
                if (j == 0)
                {
                    spread = 1.0;
                    adjacentFrom = 1.0;
                    adjacentDecay = Hypot(i, 0.5) - i;
                    oppositeDecay = 1.0;
                    return;
                }
                // i should never be 0

                double slope = (j - 0.5) / (i - 0.5);
                if (slope == 1.0)
                    spread = 0.5;
                else if (slope > 1.0)
                    spread = 0.5 / slope;
                else
                    spread = 1.0 - slope / 2.0;

                if (slope == 1.0)
                {
                    adjacentFrom = 1.0;
                    adjacentDecay = Math.Sqrt(2.0) / 2.0;
                    oppositeDecay = 1.0;
                }
                else if (slope < 1.0)
                {
                    adjacentFrom = 1.0;
                    adjacentDecay = Hypot(1.0, slope) / 2.0;
                    oppositeDecay = Hypot(1.0, slope);
                }
                else
                {
                    adjacentFrom = 1.0 / slope;
                    adjacentDecay = Hypot(1.0 / slope, 1) / 2.0;
                    oppositeDecay = 1.0;
                }
            }

            values = new LightingSpread[MAX_LIGHT_RANGE + 1, MAX_LIGHT_RANGE + 1];
            DistanceCache[,] distances = new DistanceCache[MAX_LIGHT_RANGE + 1, MAX_LIGHT_RANGE + 1];

            for (int i = 1; i <= MAX_LIGHT_RANGE; ++i)
            {
                CalculateLeftStats(i, 0,
                    out double lightFromLeft, out double topLightFromLeft,
                    out double leftToTopDistance, out double leftToRightDistance);
                values[i, 0] = new LightingSpread(
                    (float)lightFromLeft,
                    (float)topLightFromLeft,
                    DoubleToIndex(leftToTopDistance),
                    DoubleToIndex(1.0),
                    0f,
                    0f,
                    DoubleToIndex(1.0),
                    DoubleToIndex(1.0)
                );
                distances[i, 0] = new DistanceCache(i + leftToTopDistance, i + DoubleToIndex(leftToTopDistance) / 100.0, i, i);
            }

            for (int j = 1; j <= MAX_LIGHT_RANGE; ++j)
            {
                CalculateLeftStats(j, 0,
                    out double lightFromBottom, out double rightLightFromBottom,
                    out double bottomToRightDistance, out double bottomToLeftDistance);
                values[0, j] = new LightingSpread(
                    0f,
                    0f,
                    DoubleToIndex(1.0),
                    DoubleToIndex(1.0),
                    (float)lightFromBottom,
                    (float)rightLightFromBottom,
                    DoubleToIndex(bottomToRightDistance),
                    DoubleToIndex(1.0)
                );
                distances[0, j] = new DistanceCache(j, j, j + bottomToRightDistance, j + DoubleToIndex(bottomToRightDistance) / 100.0);
            }

            for (int j = 1; j <= MAX_LIGHT_RANGE; ++j)
            {
                for (int i = 1; i <= MAX_LIGHT_RANGE; ++i)
                {
                    CalculateLeftStats(
                        i, j,
                        out double lightFromLeft, out double topLightFromLeft,
                        out double leftToTopDistance, out double leftToRightDistance);
                    CalculateLeftStats(j, i,
                        out double ligthFromBottom, out double rightLightFromBottom,
                        out double bottomToRightDistance, out double bottomToTopDistance);

                    double error = lightFromLeft * distances[i - 1, j].rightRounded + ligthFromBottom * distances[i, j - 1].topRounded - Hypot(i, j);
                    leftToTopDistance -= error;
                    bottomToTopDistance -= error;
                    bottomToRightDistance -= error;
                    leftToRightDistance -= error;

                    leftToTopDistance += distances[i - 1, j].right - distances[i - 1, j].rightRounded;
                    if (rightLightFromBottom != 1.0) leftToRightDistance += distances[i - 1, j].right - distances[i - 1, j].rightRounded;
                    bottomToRightDistance += distances[i, j - 1].top - distances[i, j - 1].topRounded;
                    if (topLightFromLeft != 1.0) bottomToTopDistance += distances[i, j - 1].top - distances[i, j - 1].topRounded;

                    distances[i, j] = new DistanceCache(
                        topLightFromLeft * (leftToTopDistance + distances[i - 1, j].right)
                            + (1 - topLightFromLeft) * (bottomToTopDistance + distances[i, j - 1].top),
                        topLightFromLeft * (DoubleToIndex(leftToTopDistance) / 100.0 + distances[i - 1, j].rightRounded)
                            + (1 - topLightFromLeft) * (DoubleToIndex(bottomToTopDistance) / 100.0 + distances[i, j - 1].topRounded),
                        rightLightFromBottom * (bottomToRightDistance + distances[i, j - 1].top)
                            + (1 - rightLightFromBottom) * (leftToRightDistance + distances[i - 1, j].right),
                        rightLightFromBottom * (DoubleToIndex(bottomToRightDistance) / 100.0 + distances[i, j - 1].topRounded)
                            + (1 - rightLightFromBottom) * (DoubleToIndex(leftToRightDistance) / 100.0 + distances[i - 1, j].rightRounded)
                    );

                    values[i, j] = new LightingSpread(
                        (float)lightFromLeft,
                        (float)topLightFromLeft,
                        DoubleToIndex(leftToTopDistance),
                        DoubleToIndex(leftToRightDistance),
                        (float)ligthFromBottom,
                        (float)rightLightFromBottom,
                        DoubleToIndex(bottomToRightDistance),
                        DoubleToIndex(bottomToTopDistance)
                    );
                }
            }
        }

        internal void SpreadLight(LightMap lightMap, Vector3[] colors, LightMaskMode[] lightDecay, int width, int height)
        {
            _lightLossExitingSolid = FancyLightingMod.FancyLightingEngineLightLoss;
            if (FancyLightingMod.FancyLightingEngineUseTemporal)
            {
                _brightnessCutoff = (float)Math.Clamp(Math.Sqrt(_temporalData / 1228.8) * 0.02, 0.02, 0.125);
            } else
            {
                _brightnessCutoff = 0.04f;
            }
            _temporalData = 0;

            float decayMult = FancyLightingMod.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
            float lightAirDecayBaseline = decayMult * Math.Min(lightMap.LightDecayThroughAir, 0.97f);
            float lightSolidDecayBaseline = decayMult * Math.Min(lightMap.LightDecayThroughSolid, 0.97f);
            float lightWaterDecayBaseline = decayMult * Math.Min(
                0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length() + 0.375f * Math.Max(
                        lightMap.LightDecayThroughWater.X, Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
                    ),
                0.97f);
            float lightHoneyDecayBaseline = decayMult * Math.Min(
                0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length() + 0.375f * Math.Max(
                        lightMap.LightDecayThroughHoney.X, Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
                    ),
                0.97f);
            _logSlowestDecay = Math.Log(
                Math.Max(lightAirDecayBaseline, Math.Max(lightWaterDecayBaseline, Math.Max(lightHoneyDecayBaseline, lightSolidDecayBaseline)))
            );

            if (lightAirDecayBaseline != _lightAirDecay[100])
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    _lightAirDecay[exponent] = (float)Math.Pow(lightAirDecayBaseline, exponent / 100.0);
                }
                _lightAirDecay[100] = lightAirDecayBaseline;
            }
            if (lightSolidDecayBaseline != _lightSolidDecay[100])
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    _lightSolidDecay[exponent] = (float)Math.Pow(lightSolidDecayBaseline, exponent / 100.0);
                }
                _lightSolidDecay[100] = lightSolidDecayBaseline;
            }
            if (lightWaterDecayBaseline != _lightWaterDecay[100])
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    _lightWaterDecay[exponent] = (float)Math.Pow(lightWaterDecayBaseline, exponent / 100.0);
                }
                _lightWaterDecay[100] = lightWaterDecayBaseline;
            }
            if (lightHoneyDecayBaseline != _lightHoneyDecay[100])
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    _lightHoneyDecay[exponent] = (float)Math.Pow(lightHoneyDecayBaseline, exponent / 100.0);
                }
                _lightHoneyDecay[100] = lightHoneyDecayBaseline;
            }

            int length = width * height;

            if (_tmp is null || _tmp.Length < length)
            {
                _tmp = new Vector3[length];
                _lightMask = new float[length][];
            }

            Array.Fill(_tmp, Vector3.Zero, 0, length);

            Parallel.For(
                0,
                width,
                new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                (i) =>
                {
                    int endIndex = height * (i + 1);
                    for (int j = height * i; j < endIndex; ++j)
                    {
                        switch (lightDecay[j])
                        {
                            case LightMaskMode.None:
                            default:
                                _lightMask[j] = _lightAirDecay;
                                break;
                            case LightMaskMode.Solid:
                                int x = j / height + _lightMapArea.X;
                                int y = j % height + _lightMapArea.Y;
                                // Check Shadow Paint
                                if (Main.tile[x, y].TileColor == PaintID.ShadowPaint)
                                    _lightMask[j] = _lightShadowPaintDecay;
                                else
                                    _lightMask[j] = _lightSolidDecay;
                                break;
                            case LightMaskMode.Water:
                                _lightMask[j] = _lightWaterDecay;
                                break;
                            case LightMaskMode.Honey:
                                _lightMask[j] = _lightHoneyDecay;
                                break;
                        }

                    }
                }
            );

            Parallel.For(
                0, 
                length, 
                new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount }, 
                (i) => ProcessLightThreaded(i, colors, width, height)
            );

            Array.Copy(_tmp, colors, length);
        }

        internal void ProcessLightThreaded(int index, Vector3[] colors, int width, int height)
        {
            Vector3 color = colors[index];
            if (color.X <= 0f && color.Y <= 0f && color.Z <= 0f) return;

            void SetLightMap(int i, float value)
            {
                ref Vector3 light = ref _tmp[i];
                Vector3 newLight;
                Vector3.Multiply(ref color, value, out newLight);
                float oldValue;
                do
                {
                    oldValue = light.X;
                    if (oldValue >= newLight.X) break;
                }
                while (Interlocked.CompareExchange(ref light.X, newLight.X, oldValue) != oldValue);
                do
                {
                    oldValue = light.Y;
                    if (oldValue >= newLight.Y) break;
                }
                while (Interlocked.CompareExchange(ref light.Y, newLight.Y, oldValue) != oldValue);
                do
                {
                    oldValue = light.Z;
                    if (oldValue >= newLight.Z) break;
                }
                while (Interlocked.CompareExchange(ref light.Z, newLight.Z, oldValue) != oldValue);
            }

            SetLightMap(index, 1f);

            float threshold = _lightMask[index][150];
            int length = width * height;

            int topEdge = height * (index / height);
            int bottomEdge = topEdge + (height - 1);

            bool skipUp = false, skipDown = false, skipLeft = false, skipRight = false;

            if (index == topEdge)
            {
                skipUp = true;
            }
            else
            {
                Vector3 otherColor = (_lightMask[index - 1][100] / threshold) * colors[index - 1];
                skipUp = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
            }
            if (index == bottomEdge)
            {
                skipDown = true;
            }
            else
            {
                Vector3 otherColor = (_lightMask[index + 1][100] / threshold) * colors[index + 1];
                skipDown = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
            }
            if (index < height)
            {
                skipLeft = true;
            }
            else
            {
                Vector3 otherColor = (_lightMask[index - height][100] / threshold) * colors[index - height];
                skipLeft = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
            }
            if (index + height >= length)
            {
                skipRight = true;
            }
            else
            {
                Vector3 otherColor = (_lightMask[index + height][100] / threshold) * colors[index + height];
                skipRight = otherColor.X >= color.X && otherColor.Y >= color.Y && otherColor.Z >= color.Z;
            }

            // We blend by taking the max of each component, so this is a valid check to skip
            if (skipUp && skipDown && skipLeft && skipRight)
                return;

            float initialDecay = _lightMask[index][100];
            int lightRange = Math.Clamp(
                (int)Math.Ceiling(Math.Log(_brightnessCutoff / (initialDecay * Math.Max(color.X, Math.Max(color.Y, color.Z)))) / _logSlowestDecay) + 1,
                1, MAX_LIGHT_RANGE
            );

            // Up
            if (!skipUp)
            {
                float lightValue = 1f;
                int i = index;
                for (int y = 1; y <= lightRange; ++y)
                {
                    if (--i < topEdge) break;

                    lightValue *= _lightMask[i + 1][100];
                    if (y > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                        lightValue *= _lightLossExitingSolid;

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
                    if (++i > bottomEdge) break;

                    lightValue *= _lightMask[i - 1][100];
                    if (y > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                        lightValue *= _lightLossExitingSolid;

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
                    if ((i -= height) < 0) break;

                    lightValue *= _lightMask[i + height][100];
                    if (x > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i + height] == _lightSolidDecay)
                        lightValue *= _lightLossExitingSolid;

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
                    if ((i += height) >= length) break;

                    lightValue *= _lightMask[i - height][100];
                    if (x > 1 && _lightMask[i] == _lightAirDecay && _lightMask[i - height] == _lightSolidDecay)
                        lightValue *= _lightLossExitingSolid;

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

            var circle = _circles[lightRange];

            // precomputedLightingSpread[,]: 2D arrays in C# are stored such that blocks with a constant first index are stored contiguously
            // precomputedLightingSpread uses y as the second index, and Terraria's 1D arrays for lighting use height * x + y as the index
            // So looping over y in the inner loop should be faster and simpler

            if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
            {
                lock (_multithreadLightMap[index % _multithreadLightMap.Length])
                {
                    float[] workingLights = _multithreadLightMap[index % _multithreadLightMap.Length];

                    // Upper Right
                    if (doUpperRight)
                    {
                        workingLights[0] = initialDecay;
                        float value = 1f;
                        for (int i = index, y = 1; y <= topEdge; ++y)
                        {
                            value *= _lightMask[i][100];
                            var mask = _lightMask[--i];

                            if (y > 1 && mask == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                                value *= _lightLossExitingSolid;
                            workingLights[y] = value * mask[_precomputedLightingSpread[0, y].BottomToRightDistance];
                        }
                        for (int x = 1; x <= rightEdge; ++x)
                        {
                            int i = index + height * x;
                            var mask = _lightMask[i];

                            float verticalLight = workingLights[0] * mask[_precomputedLightingSpread[x, 0].LeftToTopDistance];
                            workingLights[0] *= mask[100];
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
                                        verticalLight *= _lightLossExitingSolid;
                                    if (_lightMask[i - height] == _lightSolidDecay)
                                        horizontalLight *= _lightLossExitingSolid;
                                }
                                ref LightingSpread spread = ref _precomputedLightingSpread[x, y];
                                SetLightMap(i,
                                      spread.LightFromBottom * verticalLight
                                    + spread.LightFromLeft * horizontalLight
                                );
                                workingLights[y] = spread.RightLightFromBottom * verticalLight * mask[spread.BottomToRightDistance]
                                                   + (1 - spread.RightLightFromBottom) * horizontalLight * mask[spread.LeftToRightDistance];
                                verticalLight = spread.TopLightFromLeft * horizontalLight * mask[spread.LeftToTopDistance]
                                                + (1 - spread.TopLightFromLeft) * verticalLight * mask[spread.BottomToTopDistance];
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
                            value *= _lightMask[i][100];
                            var mask = _lightMask[--i];

                            if (y > 1 && mask == _lightAirDecay && _lightMask[i + 1] == _lightSolidDecay)
                                value *= _lightLossExitingSolid;
                            workingLights[y] = value * mask[_precomputedLightingSpread[0, y].BottomToRightDistance];
                        }
                        for (int x = 1; x <= leftEdge; ++x)
                        {
                            int i = index - height * x;
                            var mask = _lightMask[i];

                            float verticalLight = workingLights[0] * mask[_precomputedLightingSpread[x, 0].LeftToTopDistance];
                            workingLights[0] *= mask[100];
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
                                        verticalLight *= _lightLossExitingSolid;
                                    if (_lightMask[i + height] == _lightSolidDecay)
                                        horizontalLight *= _lightLossExitingSolid;
                                }
                                ref LightingSpread spread = ref _precomputedLightingSpread[x, y];
                                SetLightMap(i,
                                      spread.LightFromBottom * verticalLight
                                    + spread.LightFromLeft * horizontalLight
                                );
                                workingLights[y] = spread.RightLightFromBottom * verticalLight * mask[spread.BottomToRightDistance]
                                                   + (1 - spread.RightLightFromBottom) * horizontalLight * mask[spread.LeftToRightDistance];
                                verticalLight = spread.TopLightFromLeft * horizontalLight * mask[spread.LeftToTopDistance]
                                                + (1 - spread.TopLightFromLeft) * verticalLight * mask[spread.BottomToTopDistance];
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
                            value *= _lightMask[i][100];
                            var mask = _lightMask[++i];

                            if (y > 1 && mask == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                                value *= _lightLossExitingSolid;
                            workingLights[y] = value * mask[_precomputedLightingSpread[0, y].BottomToRightDistance];
                        }
                        for (int x = 1; x <= rightEdge; ++x)
                        {
                            int i = index + height * x;
                            var mask = _lightMask[i];

                            float verticalLight = workingLights[0] * mask[_precomputedLightingSpread[x, 0].LeftToTopDistance];
                            workingLights[0] *= mask[100];
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
                                        verticalLight *= _lightLossExitingSolid;
                                    if (_lightMask[i - height] == _lightSolidDecay)
                                        horizontalLight *= _lightLossExitingSolid;
                                }
                                ref LightingSpread spread = ref _precomputedLightingSpread[x, y];
                                SetLightMap(i,
                                      spread.LightFromBottom * verticalLight
                                    + spread.LightFromLeft * horizontalLight
                                );
                                workingLights[y] = spread.RightLightFromBottom * verticalLight * mask[spread.BottomToRightDistance]
                                                   + (1 - spread.RightLightFromBottom) * horizontalLight * mask[spread.LeftToRightDistance];
                                verticalLight = spread.TopLightFromLeft * horizontalLight * mask[spread.LeftToTopDistance]
                                                + (1 - spread.TopLightFromLeft) * verticalLight * mask[spread.BottomToTopDistance];
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
                            value *= _lightMask[i][100];
                            var mask = _lightMask[++i];

                            if (y > 1 && mask == _lightAirDecay && _lightMask[i - 1] == _lightSolidDecay)
                                value *= _lightLossExitingSolid;
                            workingLights[y] = value * mask[_precomputedLightingSpread[0, y].BottomToRightDistance];
                        }
                        for (int x = 1; x <= leftEdge; ++x)
                        {
                            int i = index - height * x;
                            var mask = _lightMask[i];

                            float verticalLight = workingLights[0] * mask[_precomputedLightingSpread[x, 0].LeftToTopDistance];
                            workingLights[0] *= mask[100];
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
                                        verticalLight *= _lightLossExitingSolid;
                                    if (_lightMask[i + height] == _lightSolidDecay)
                                        horizontalLight *= _lightLossExitingSolid;
                                }
                                ref LightingSpread spread = ref _precomputedLightingSpread[x, y];
                                SetLightMap(i,
                                      spread.LightFromBottom * verticalLight
                                    + spread.LightFromLeft * horizontalLight
                                );
                                workingLights[y] = spread.RightLightFromBottom * verticalLight * mask[spread.BottomToRightDistance]
                                                   + (1 - spread.RightLightFromBottom) * horizontalLight * mask[spread.LeftToRightDistance];
                                verticalLight = spread.TopLightFromLeft * horizontalLight * mask[spread.LeftToTopDistance]
                                                + (1 - spread.TopLightFromLeft) * verticalLight * mask[spread.BottomToTopDistance];
                            }
                        }
                    }
                }
            }

            if (FancyLightingMod.FancyLightingEngineUseTemporal)
            {
                int approximateWorkDone = 0;
                if (!skipUp) approximateWorkDone += 1;
                if (!skipDown) approximateWorkDone += 1;
                if (!skipLeft) approximateWorkDone += 1;
                if (!skipRight) approximateWorkDone += 1;
                if (doUpperRight) approximateWorkDone += 20;
                if (doUpperLeft) approximateWorkDone += 20;
                if (doLowerRight) approximateWorkDone += 20;
                if (doLowerLeft) approximateWorkDone += 20;
                Interlocked.Add(ref _temporalData, approximateWorkDone);
            }
        }
    }
}
