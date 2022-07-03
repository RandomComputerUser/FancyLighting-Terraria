using Terraria;
using Terraria.ID;
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

        const int MAX_LIGHT_RANGE = 36;
        protected float lightAirDecayCache;
        protected float lightSolidDecayCache;
        protected float[] lightAirDecay;
        protected float[] lightSolidDecay;
        protected float[] lightWaterDecay;
        protected float[] lightHoneyDecay;
        protected float[] lightShadowPaintDecay; // In vanilla shadow paint isn't a special case
        protected float brightnessCutoff;

        protected float lightLossExitingSolid;

        protected LightingSpread[,] precomputedLightingSpread;
        protected float[,] LightingMap;
        protected float[][] MultithreadLightMap;

        private Vector3[] _tmp;
        private float[][] _lightMask;
        private object[] _locks;

        internal Rectangle lightMapArea;

        protected int temporalData;
        private object _temporalLock;

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
                lightShadowPaintDecay[exponent] = (float)Math.Pow(0.175, exponent / 100.0);
            }

            MultithreadLightMap = new float[256][];
            for (int i = 0; i < 256; i++)
            {
                MultithreadLightMap[i] = new float[MAX_LIGHT_RANGE + 1];
            }

            temporalData = 0;
            _temporalLock = new object();
        }

        protected void ComputeLightingSpread(ref LightingSpread[,] values)
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
                    adjacentDecay = Hypot(1, (j + 0.5) / (i + 0.5)) / 2.0;
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
            lightLossExitingSolid = FancyLightingMod.FancyLightingEngineLightLoss;
            if (FancyLightingMod.FancyLightingEngineUseTemporal)
            {
                brightnessCutoff = (float)Math.Clamp(Math.Sqrt(temporalData / 3000.0) * 0.03125, 0.03125, 0.1);
            } else
            {
                brightnessCutoff = 0.04f;
            }
            temporalData = 0;

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
                width,
                new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                (i) =>
                {
                    for (int j = height * i; j < height * (i + 1); ++j)
                    {
                        switch (lightDecay[j])
                        {
                            case LightMaskMode.None:
                            default:
                                _lightMask[j] = lightAirDecay;
                                break;
                            case LightMaskMode.Solid:
                                int x = j / height + lightMapArea.X;
                                int y = j % height + lightMapArea.Y;
                                // Check Shadow Paint
                                if (Main.tile[x, y].TileColor == PaintID.ShadowPaint)
                                    _lightMask[j] = lightShadowPaintDecay;
                                else
                                    _lightMask[j] = lightSolidDecay;
                                break;
                            case LightMaskMode.Water:
                                _lightMask[j] = lightWaterDecay;
                                break;
                            case LightMaskMode.Honey:
                                _lightMask[j] = lightHoneyDecay;
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

            float initialDecay = _lightMask[index][100];
            float maxComponent = Math.Max(color.X, Math.Max(color.Y, color.Z)) * initialDecay;
            int lightRange = Math.Clamp(
                (int)Math.Ceiling(Math.Log(brightnessCutoff / maxComponent) / Math.Log(Math.Max(lightAirDecay[100], lightWaterDecay[100]))) + 1,
                1, MAX_LIGHT_RANGE
            );

            float[] workingLights;

            void SetLightMap(int i, float value)
            {
                ref Vector3 light = ref _tmp[i];
                lock (_locks[i])
                {
                    if (value * color.X > light.X) light.X = value * color.X;
                    if (value * color.Y > light.Y) light.Y = value * color.Y;
                    if (value * color.Z > light.Z) light.Z = value * color.Z;
                }
            }

            SetLightMap(index, 1f);

            float threshold = _lightMask[index][50];
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

            float lightValue;

            // Up
            if (!up)
            {
                lightValue = 1f;
                int i = index;
                for (int y = 1; y <= lightRange; ++y)
                {
                    if (--i < 0) break;

                    lightValue *= _lightMask[i + 1][100];
                    if (y > 1 && _lightMask[i] == lightAirDecay && _lightMask[i + 1] == lightSolidDecay)
                        lightValue *= lightLossExitingSolid;

                    SetLightMap(i, lightValue);
                }
            }

            // Down
            if (!down)
            {
                lightValue = 1f;
                int i = index;
                for (int y = 1; y <= lightRange; ++y)
                {
                    if (++i >= length) break;

                    lightValue *= _lightMask[i - 1][100];
                    if (y > 1 && _lightMask[i] == lightAirDecay && _lightMask[i - 1] == lightSolidDecay)
                        lightValue *= lightLossExitingSolid;

                    SetLightMap(i, lightValue);
                }
            }

            // Left
            if (!left)
            {
                lightValue = 1f;
                int i = index;
                for (int x = 1; x <= lightRange; ++x)
                {
                    if ((i -= height) < 0) break;

                    lightValue *= _lightMask[i + height][100];
                    if (x > 1 && _lightMask[i] == lightAirDecay && _lightMask[i + height] == lightSolidDecay)
                        lightValue *= lightLossExitingSolid;

                    SetLightMap(i, lightValue);
                }
            }

            // Right
            if (!right)
            {
                lightValue = 1f;
                int i = index;
                for (int x = 1; x <= lightRange; ++x)
                {
                    if ((i += height) >= length) break;

                    lightValue *= _lightMask[i - height][100];
                    if (x > 1 && _lightMask[i] == lightAirDecay && _lightMask[i - height] == lightSolidDecay)
                        lightValue *= lightLossExitingSolid;

                    SetLightMap(i, lightValue);
                }
            }

            int midX = index / height;
            int midY = index % height;

            int topEdge = Math.Min(midY, lightRange);
            int bottomEdge = Math.Min(height - 1 - midY, lightRange);
            int leftEdge = Math.Min(midX, lightRange);
            int rightEdge = Math.Min(width - 1 - midX, lightRange);

            // precomputedLightingSpread[,]: 2D arrays in C# are stored such that blocks with a constant first index are stored contiguously
            // precomputedLightingSpread uses y as the second index, and Terraria's 1D arrays for lighting use height * x + y as the index
            // So looping over y in the inner loop should be faster and simpler

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
                    for (int i = index, y = 1; y <= topEdge; ++y)
                    {
                        value *= _lightMask[i][100];
                        var mask = _lightMask[--i];

                        if (y > 1 && mask == lightAirDecay && _lightMask[i + 1] == lightSolidDecay)
                            value *= lightLossExitingSolid;
                        workingLights[y] = value * mask[precomputedLightingSpread[0, y].b2rDecay];
                    }
                    for (int x = 1; x <= rightEdge; ++x)
                    {
                        int i = index + height * x;
                        var mask = _lightMask[i];

                        float verticalLight = workingLights[0] * mask[precomputedLightingSpread[x, 0].l2tDecay];
                        workingLights[0] *= mask[100];
                        if (x > 1 && mask == lightAirDecay && _lightMask[i - height] == lightSolidDecay)
                        {
                            verticalLight *= lightLossExitingSolid;
                            workingLights[0] *= lightLossExitingSolid;
                        }

                        for (int y = 1; y <= topEdge; ++y)
                        {
                            mask = _lightMask[--i];

                            if (mask == lightAirDecay)
                            {
                                if (_lightMask[i + 1] == lightSolidDecay) verticalLight *= lightLossExitingSolid;
                                if (_lightMask[i - height] == lightSolidDecay) workingLights[y] *= lightLossExitingSolid;
                            }
                            ref LightingSpread spread = ref precomputedLightingSpread[x, y];
                            SetLightMap(i,
                                  spread.bottom * verticalLight
                                + spread.left * workingLights[y]
                            );
                            (verticalLight, workingLights[y]) =
                            (
                                spread.topFromLeft * workingLights[y] * mask[spread.l2tDecay] + (1 - spread.topFromLeft) * verticalLight * mask[spread.b2tDecay],
                                spread.rightFromBottom * verticalLight * mask[spread.b2rDecay] + (1 - spread.rightFromBottom) * workingLights[y] * mask[spread.l2rDecay]
                            );
                        }
                    }
                }

                // Upper Left
                if (!(up || left))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int i = index, y = 1; y <= topEdge; ++y)
                    {
                        value *= _lightMask[i][100];
                        var mask = _lightMask[--i];

                        if (y > 1 && mask == lightAirDecay && _lightMask[i + 1] == lightSolidDecay)
                            value *= lightLossExitingSolid;
                        workingLights[y] = value * mask[precomputedLightingSpread[0, y].b2rDecay];
                    }
                    for (int x = 1; x <= leftEdge; ++x)
                    {
                        int i = index - height * x;
                        var mask = _lightMask[i];

                        float verticalLight = workingLights[0] * mask[precomputedLightingSpread[x, 0].l2tDecay];
                        workingLights[0] *= mask[100];
                        if (x > 1 && mask == lightAirDecay && _lightMask[i + height] == lightSolidDecay)
                        {
                            verticalLight *= lightLossExitingSolid;
                            workingLights[0] *= lightLossExitingSolid;
                        }

                        for (int y = 1; y <= topEdge; ++y)
                        {
                            mask = _lightMask[--i];

                            if (mask == lightAirDecay)
                            {
                                if (_lightMask[i + 1] == lightSolidDecay) verticalLight *= lightLossExitingSolid;
                                if (_lightMask[i + height] == lightSolidDecay) workingLights[y] *= lightLossExitingSolid;
                            }
                            ref LightingSpread spread = ref precomputedLightingSpread[x, y];
                            SetLightMap(i,
                                  spread.bottom * verticalLight
                                + spread.left * workingLights[y]
                            );
                            (verticalLight, workingLights[y]) =
                            (
                                spread.topFromLeft * workingLights[y] * mask[spread.l2tDecay] + (1 - spread.topFromLeft) * verticalLight * mask[spread.b2tDecay],
                                spread.rightFromBottom * verticalLight * mask[spread.b2rDecay] + (1 - spread.rightFromBottom) * workingLights[y] * mask[spread.l2rDecay]
                            );
                        }
                    }
                }

                // Lower Right
                if (!(down || right))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int i = index, y = 1; y <= bottomEdge; ++y)
                    {
                        value *= _lightMask[i][100];
                        var mask = _lightMask[++i];

                        if (y > 1 && mask == lightAirDecay && _lightMask[i - 1] == lightSolidDecay)
                            value *= lightLossExitingSolid;
                        workingLights[y] = value * mask[precomputedLightingSpread[0, y].b2rDecay];
                    }
                    for (int x = 1; x <= rightEdge; ++x)
                    {
                        int i = index + height * x;
                        var mask = _lightMask[i];

                        float verticalLight = workingLights[0] * mask[precomputedLightingSpread[x, 0].l2tDecay];
                        workingLights[0] *= mask[100];
                        if (x > 1 && mask == lightAirDecay && _lightMask[i - height] == lightSolidDecay)
                        {
                            verticalLight *= lightLossExitingSolid;
                            workingLights[0] *= lightLossExitingSolid;
                        }

                        for (int y = 1; y <= bottomEdge; ++y)
                        {
                            mask = _lightMask[++i];

                            if (mask == lightAirDecay)
                            {
                                if (_lightMask[i - 1] == lightSolidDecay) verticalLight *= lightLossExitingSolid;
                                if (_lightMask[i - height] == lightSolidDecay) workingLights[y] *= lightLossExitingSolid;
                            }
                            ref LightingSpread spread = ref precomputedLightingSpread[x, y];
                            SetLightMap(i,
                                  spread.bottom * verticalLight
                                + spread.left * workingLights[y]
                            );
                            (verticalLight, workingLights[y]) =
                            (
                                spread.topFromLeft * workingLights[y] * mask[spread.l2tDecay] + (1 - spread.topFromLeft) * verticalLight * mask[spread.b2tDecay],
                                spread.rightFromBottom * verticalLight * mask[spread.b2rDecay] + (1 - spread.rightFromBottom) * workingLights[y] * mask[spread.l2rDecay]
                            );
                        }
                    }
                }

                // Lower Left
                if (!(down || left))
                {
                    workingLights[0] = initialDecay;
                    float value = 1f;
                    for (int i = index, y = 1; y <= bottomEdge; ++y)
                    {
                        value *= _lightMask[i][100];
                        var mask = _lightMask[++i];

                        if (y > 1 && mask == lightAirDecay && _lightMask[i - 1] == lightSolidDecay)
                            value *= lightLossExitingSolid;
                        workingLights[y] = value * mask[precomputedLightingSpread[0, y].b2rDecay];
                    }
                    for (int x = 1; x <= leftEdge; ++x)
                    {
                        int i = index - height * x;
                        var mask = _lightMask[i];

                        float verticalLight = workingLights[0] * mask[precomputedLightingSpread[x, 0].l2tDecay];
                        workingLights[0] *= mask[100];
                        if (x > 1 && mask == lightAirDecay && _lightMask[i + height] == lightSolidDecay)
                        {
                            verticalLight *= lightLossExitingSolid;
                            workingLights[0] *= lightLossExitingSolid;
                        }

                        for (int y = 1; y <= bottomEdge; ++y)
                        {
                            mask = _lightMask[++i];

                            if (mask == lightAirDecay)
                            {
                                if (_lightMask[i - 1] == lightSolidDecay) verticalLight *= lightLossExitingSolid;
                                if (_lightMask[i + height] == lightSolidDecay) workingLights[y] *= lightLossExitingSolid;
                            }
                            ref LightingSpread spread = ref precomputedLightingSpread[x, y];
                            SetLightMap(i,
                                  spread.bottom * verticalLight
                                + spread.left * workingLights[y]
                            );
                            (verticalLight, workingLights[y]) =
                            (
                                spread.topFromLeft * workingLights[y] * mask[spread.l2tDecay] + (1 - spread.topFromLeft) * verticalLight * mask[spread.b2tDecay],
                                spread.rightFromBottom * verticalLight * mask[spread.b2rDecay] + (1 - spread.rightFromBottom) * workingLights[y] * mask[spread.l2rDecay]
                            );
                        }
                    }
                }

            }

            if (FancyLightingMod.FancyLightingEngineUseTemporal)
            {
                int approximateWorkDone = 0;
                if (!up) approximateWorkDone += 1;
                if (!down) approximateWorkDone += 1;
                if (!left) approximateWorkDone += 1;
                if (!right) approximateWorkDone += 1;
                if (!(up || right)) approximateWorkDone += 20;
                if (!(up || left)) approximateWorkDone += 20;
                if (!(down || right)) approximateWorkDone += 20;
                if (!(down || left)) approximateWorkDone += 20;
                lock (_temporalLock)
                {
                    temporalData += approximateWorkDone;
                }
            }
        }
    }
}
