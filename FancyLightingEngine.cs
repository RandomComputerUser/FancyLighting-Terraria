using Terraria;
using Terraria.Graphics.Light;

using Microsoft.Xna.Framework;

using System;
using System.Threading.Tasks;

namespace FancyLighting
{

    class FancyLightingEngine
    {
        protected record struct LightingSpread(
            float left,
            float topFromLeft,
            int l2tDecay,
            int l2rDecay,
            float bottom,
            float rightFromBottom,
            int b2rDecay,
            int b2tDecay
        );

        private record struct DistanceCache(double top, double right);

        const int MAX_LIGHT_RANGE = 36;
        protected float lightAirDecayCache;
        protected float lightSolidDecayCache;
        protected float[] lightAirDecay;
        protected float[] lightSolidDecay;
        protected float[] lightWaterDecay;
        protected float[] lightHoneyDecay;
        protected float[] lightShadowPaintDecay; // In vanilla shadow paint isn't a special case

        protected LightingSpread[,] precomputedLightingSpread;
        protected float[,] LightingMap;
        protected float[][] MultithreadLightMap;

        private Vector3[] _tmp;
        private float[][] _lightMask;
        private object[] _locks;

        internal Rectangle lightMapArea;

        public FancyLightingEngine() {
            ComputeLightingSpread(ref precomputedLightingSpread);

            LightingMap = new float[2 * MAX_LIGHT_RANGE + 1, 2 * MAX_LIGHT_RANGE + 1];

            lightAirDecayCache = 0f;
            lightSolidDecayCache = 0f;

            lightAirDecay = new float[151];
            lightSolidDecay = new float[151];
            lightWaterDecay = new float[151];
            lightHoneyDecay = new float[151];
            lightShadowPaintDecay = new float[151];
            for (int exponent = 0; exponent <= 150; ++exponent)
            {
                lightAirDecay[exponent] = 1f;
                lightSolidDecay[exponent] = 1f;
                lightWaterDecay[exponent] = (float)Math.Pow(0.87, exponent / 100.0);
                lightHoneyDecay[exponent] = (float)Math.Pow(0.75, exponent / 100.0);
                lightShadowPaintDecay[exponent] = (float)Math.Pow(0.25, exponent / 100.0);
            }

            MultithreadLightMap = new float[128][];
            for (int i = 0; i < 128; i++)
            {
                MultithreadLightMap[i] = new float[MAX_LIGHT_RANGE + 1];
            }
        }

        protected void ComputeLightingSpread(ref LightingSpread[,] values)
        {
            double Hypot(double x, double y)
            {
                return Math.Sqrt(x * x + y * y);
            }

            int DoubleToIndex(double x)
            {
                return Math.Clamp((int)(100.0 * x), 0, 150);
            }

            void CalculateLeftStats(int i, int j, out double spread, out double adjacentFrom, out double adjacentDecay, out double oppositeDecay)
            {
                if (j == 0)
                {
                    spread = 1.0;
                    adjacentFrom = 1.0;
                    adjacentDecay = Hypot(1, (j + 0.5) / (i + 0.5)) / 2.0;
                    oppositeDecay = 1.0;
                    return;
                }
                // i should never be 0

                double slope = (j - 0.5) / (i - 0.5);
                if (slope == 1.0)
                    spread = 0.5f;
                else if (slope > 1.0)
                    spread = 0.5 / slope;
                else
                    spread = 1.0 - slope / 2.0;

                if (slope <= 1.0)
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

            for (int i = 1; i <= MAX_LIGHT_RANGE; ++i)
            {
                double left, topFromLeft, l2tDecay, l2rDecay;
                CalculateLeftStats(i, 0, out left, out topFromLeft, out l2tDecay, out l2rDecay);
                values[i, 0] = new LightingSpread(
                    (float)left,
                    (float)topFromLeft,
                    DoubleToIndex(l2tDecay),
                    DoubleToIndex(l2rDecay),
                    0f,
                    0f,
                    DoubleToIndex(1.0),
                    DoubleToIndex(1.0)
                );
            }

            for (int j = 1; j <= MAX_LIGHT_RANGE; ++j)
            {
                double bottom, rightFromBottom, b2rDecay, b2tDecay;
                CalculateLeftStats(j, 0, out bottom, out rightFromBottom, out b2rDecay, out b2tDecay);
                values[0, j] = new LightingSpread(
                    0f,
                    0f,
                    DoubleToIndex(1.0),
                    DoubleToIndex(1.0),
                    (float)bottom,
                    (float)rightFromBottom,
                    DoubleToIndex(b2rDecay),
                    DoubleToIndex(b2tDecay)
                );
            }

            for (int j = 1; j <= MAX_LIGHT_RANGE; ++j)
            {
                for (int i = 1; i <= MAX_LIGHT_RANGE; ++i)
                {
                    double left, topFromLeft, l2tDecay, l2rDecay;
                    CalculateLeftStats(i, j, out left, out topFromLeft, out l2tDecay, out l2rDecay);
                    double bottom, rightFromBottom, b2rDecay, b2tDecay;
                    CalculateLeftStats(j, i, out bottom, out rightFromBottom, out b2rDecay, out b2tDecay);

                    values[i, j] = new LightingSpread(
                        (float)left,
                        (float)topFromLeft,
                        DoubleToIndex(l2tDecay),
                        DoubleToIndex(l2rDecay),
                        (float)bottom,
                        (float)rightFromBottom,
                        DoubleToIndex(b2rDecay),
                        DoubleToIndex(b2tDecay)
                    );
                }
            }
        }

        internal void SpreadLight(LightMap lightMap, Vector3[] colors, LightMaskMode[] lightDecay, int width, int height)
        {
            float lightAirDecayBaseline = 0.97f * lightMap.LightDecayThroughAir;
            float lightSolidDecayBaseline = 0.97f * lightMap.LightDecayThroughSolid;

            if (lightAirDecayBaseline != lightAirDecayCache)
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    lightAirDecay[exponent] = (float)Math.Pow(lightAirDecayBaseline, exponent / 100.0);
                }
                lightAirDecayCache = lightAirDecayBaseline;
            }
            if (lightSolidDecayBaseline != lightSolidDecayCache)
            {
                for (int exponent = 0; exponent <= 150; ++exponent)
                {
                    lightSolidDecay[exponent] = (float)Math.Pow(lightSolidDecayBaseline, exponent / 100.0);
                }
                lightSolidDecayCache = lightSolidDecayBaseline;
            }


            int length = width * height;

            if (_tmp is null || _tmp.Length < length)
            {
                _tmp = new Vector3[length];
                _lightMask = new float[length][];
                _locks = new object[length];
                for (int i = 0; i < length; ++i)
                {
                    _locks[i] = new object();
                }
            }


            Array.Fill(_tmp, Vector3.Zero, 0, length);

            Parallel.For(
                0,
                length,
                new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.FancyLightingEngineThreadCount },
                (i) =>
                {
                    switch (lightDecay[i])
                    {
                        case LightMaskMode.None:
                        default:
                            _lightMask[i] = lightAirDecay;
                            break;
                        case LightMaskMode.Solid:
                            int x = i / height + lightMapArea.X;
                            int y = i % height + lightMapArea.Y;
                            // Check Shadow Paint
                            if (Main.tile[x, y].TileColor == (byte)29)
                                _lightMask[i] = lightShadowPaintDecay;
                            else
                                _lightMask[i] = lightSolidDecay;
                            break;
                        case LightMaskMode.Water:
                            _lightMask[i] = lightWaterDecay;
                            break;
                        case LightMaskMode.Honey:
                            _lightMask[i] = lightHoneyDecay;
                            break;
                    }
                }
            );

            Parallel.For(
                0, 
                length, 
                new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.FancyLightingEngineThreadCount }, 
                (i) => ProcessLightThreaded(i, colors, width, height)
            );

            Array.Copy(_tmp, colors, length);
        }

        internal void ProcessLightThreaded(int index, Vector3[] colors, int width, int height)
        {
            Vector3 color = colors[index];
            if (color == Vector3.Zero) return;

            float initialDecay = LightingDecay(index, 100);
            float maxComponent = Math.Max(color.X, Math.Max(color.Y, color.Z)) * initialDecay;
            int lightRange = Math.Clamp(
                (int)Math.Ceiling(Math.Log(0.04 / maxComponent) / Math.Log(Math.Max(lightAirDecay[100], lightWaterDecay[100]))) + 1,
                1, MAX_LIGHT_RANGE
            );

            float[] workingLights;

            void SetLightMap(int i, float value)
            {
                lock (_locks[i])
                {
                    _tmp[i] = Vector3.Max(_tmp[i], value * color);
                }
            }

            SetLightMap(index, 1f);

            float threshold = LightingDecay(index, 50);
            int length = width * height;

            bool up = index - 1 >= 0
                && colors[index - 1].X >= threshold * color.X
                && colors[index - 1].Y >= threshold * color.Y
                && colors[index - 1].Z >= threshold * color.Z;
            bool down = index + 1 < length
                && colors[index + 1].X >= threshold * color.X
                && colors[index + 1].Y >= threshold * color.Y
                && colors[index + 1].Z >= threshold * color.Z;
            bool left = index - height >= 0
                && colors[index - height].X >= threshold * color.X
                && colors[index - height].Y >= threshold * color.Y
                && colors[index - height].Z >= threshold * color.Z;
            bool right = index + height < length
                && colors[index + height].X >= threshold * color.X
                && colors[index + height].Y >= threshold * color.Y
                && colors[index + height].Z >= threshold * color.Z;

            // We blend by taking the max of each component
            if (up && down && left && right)
                return;

            float LightingDecay(int i, int exponent)
            {
                return _lightMask[i][exponent];
            }

            float lightValue;

            // Up
            if (!(up && left && right))
            {
                lightValue = 1f;
                for (int y = 1; y <= lightRange; ++y)
                {
                    int i = index - y;
                    if (i < 0) break;

                    lightValue *= LightingDecay(i + 1, 100);

                    SetLightMap(i, lightValue);
                }
            }

            // Down
            if (!(down && left && right))
            {
                lightValue = 1f;
                for (int y = 1; y <= lightRange; ++y)
                {
                    int i = index + y;
                    if (i >= length) break;

                    lightValue *= LightingDecay(i - 1, 100);

                    SetLightMap(i, lightValue);
                }
            }

            // Left
            if (!(left && up && down))
            {
                lightValue = 1f;
                for (int x = 1; x <= lightRange; ++x)
                {
                    int i = index - height * x;
                    if (i < 0) break;

                    lightValue *= LightingDecay(i + height, 100);

                    SetLightMap(i, lightValue);
                }
            }

            // Right
            if (!(right && up && down))
            {
                lightValue = 1f;
                for (int x = 1; x <= lightRange; ++x)
                {
                    int i = index + height * x;
                    if (i >= length) break;

                    lightValue *= LightingDecay(i - height, 100);

                    SetLightMap(i, lightValue);
                }
            }

            int midX = index / height;
            int midY = index % height;

            int topEdge = Math.Clamp(midY - lightRange, 0, height - 1);
            int bottomEdge = Math.Clamp(midY + lightRange, 0, height - 1);
            int leftEdge = Math.Clamp(midX - lightRange, 0, width - 1);
            int rightEdge = Math.Clamp(midX + lightRange, 0, width - 1);

            lock (MultithreadLightMap[index % MultithreadLightMap.Length])
            {
                workingLights = MultithreadLightMap[index % MultithreadLightMap.Length];

                // The culling isn't always correct but needs to be more aggressive for acceptable performance
                // 100% correct culling should use && instead of ||

                // Upper Right
                if (!(up || right))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int x = midX + 1, i = index, j = 1; x <= rightEdge; ++x, i += height, ++j)
                    {
                        value *= LightingDecay(i, 100);
                        workingLights[j] = value * LightingDecay(i, precomputedLightingSpread[j, 0].l2tDecay);
                    }
                    for (int y = midY - 1, y1 = 1; y >= topEdge; --y, ++y1)
                    {
                        int i = height * midX + y;
                        float horizontalLight = workingLights[0] * LightingDecay(i, precomputedLightingSpread[0, y1].b2rDecay);
                        workingLights[0] *= LightingDecay(i, 100);

                        for (int x = midX + 1, x1 = 1; x <= rightEdge; ++x, ++x1)
                        {
                            LightingSpread spread = precomputedLightingSpread[x1, y1];
                            SetLightMap(i += height,
                                  spread.left * horizontalLight
                                + spread.bottom * workingLights[x1]
                            );
                            (workingLights[x1], horizontalLight) =
                            (
                                spread.topFromLeft * horizontalLight * LightingDecay(i, spread.l2tDecay) + (1 - spread.topFromLeft) * workingLights[x1] * LightingDecay(i, spread.b2tDecay),
                                spread.rightFromBottom * workingLights[x1] * LightingDecay(i, spread.b2rDecay) + (1 - spread.rightFromBottom) * horizontalLight * LightingDecay(i, spread.l2rDecay)
                            );
                        }
                    }
                }

                // Upper Left
                if (!(up || left))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int x = midX - 1, i = index, j = 1; x >= leftEdge; --x, i -= height, ++j)
                    {
                        value *= LightingDecay(i, 100);
                        workingLights[j] = value * LightingDecay(i, precomputedLightingSpread[j, 0].l2tDecay);
                    }
                    for (int y = midY - 1, y1 = 1; y >= topEdge; --y, ++y1)
                    {
                        int i = height * midX + y;
                        float horizontalLight = workingLights[0] * LightingDecay(i, precomputedLightingSpread[0, y1].b2rDecay);
                        workingLights[0] *= LightingDecay(i, 100);

                        for (int x = midX - 1, x1 = 1; x >= leftEdge; --x, ++x1)
                        {
                            LightingSpread spread = precomputedLightingSpread[x1, y1];
                            SetLightMap(i -= height,
                                  spread.left * horizontalLight
                                + spread.bottom * workingLights[x1]
                            );
                            (workingLights[x1], horizontalLight) =
                            (
                                spread.topFromLeft * horizontalLight * LightingDecay(i, spread.l2tDecay) + (1 - spread.topFromLeft) * workingLights[x1] * LightingDecay(i, spread.b2tDecay),
                                spread.rightFromBottom * workingLights[x1] * LightingDecay(i, spread.b2rDecay) + (1 - spread.rightFromBottom) * horizontalLight * LightingDecay(i, spread.l2rDecay)
                            );
                        }
                    }
                }

                // Lower Right
                if (!(down || right))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int x = midX + 1, i = index, j = 1; x <= rightEdge; ++x, i += height, ++j)
                    {
                        value *= LightingDecay(i, 100);
                        workingLights[j] = value * LightingDecay(i, precomputedLightingSpread[j, 0].l2tDecay);
                    }
                    for (int y = midY + 1, y1 = 1; y <= bottomEdge; ++y, ++y1)
                    {
                        int i = height * midX + y;
                        float horizontalLight = workingLights[0] * LightingDecay(i, precomputedLightingSpread[0, y1].b2rDecay);
                        workingLights[0] *= LightingDecay(i, 100);

                        for (int x = midX + 1, x1 = 1; x <= rightEdge; ++x, ++x1)
                        {
                            LightingSpread spread = precomputedLightingSpread[x1, y1];
                            SetLightMap(i += height,
                                  spread.left * horizontalLight
                                + spread.bottom * workingLights[x1]
                            );
                            (workingLights[x1], horizontalLight) =
                            (
                                spread.topFromLeft * horizontalLight * LightingDecay(i, spread.l2tDecay) + (1 - spread.topFromLeft) * workingLights[x1] * LightingDecay(i, spread.b2tDecay),
                                spread.rightFromBottom * workingLights[x1] * LightingDecay(i, spread.b2rDecay) + (1 - spread.rightFromBottom) * horizontalLight * LightingDecay(i, spread.l2rDecay)
                            );
                        }
                    }
                }

                // Lower Left
                if (!(down || left))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int x = midX - 1, i = index, j = 1; x >= leftEdge; --x, i -= height, ++j)
                    {
                        value *= LightingDecay(i, 100);
                        workingLights[j] = value * LightingDecay(i, precomputedLightingSpread[j, 0].l2tDecay);
                    }
                    for (int y = midY + 1, y1 = 1; y <= bottomEdge; ++y, ++y1)
                    {
                        int i = height * midX + y;
                        float horizontalLight = workingLights[0] * LightingDecay(i, precomputedLightingSpread[0, y1].b2rDecay);
                        workingLights[0] *= LightingDecay(i, 100);

                        for (int x = midX - 1, x1 = 1; x >= leftEdge; --x, ++x1)
                        {
                            LightingSpread spread = precomputedLightingSpread[x1, y1];
                            SetLightMap(i -= height,
                                  spread.left * horizontalLight
                                + spread.bottom * workingLights[x1]
                            );
                            (workingLights[x1], horizontalLight) =
                            (
                                spread.topFromLeft * horizontalLight * LightingDecay(i, spread.l2tDecay) + (1 - spread.topFromLeft) * workingLights[x1] * LightingDecay(i, spread.b2tDecay),
                                spread.rightFromBottom * workingLights[x1] * LightingDecay(i, spread.b2rDecay) + (1 - spread.rightFromBottom) * horizontalLight * LightingDecay(i, spread.l2rDecay)
                            );
                        }
                    }
                }

            }
        }

    }

}
