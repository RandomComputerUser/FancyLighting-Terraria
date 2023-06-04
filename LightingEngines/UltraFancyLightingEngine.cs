using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;
using Vec4 = System.Numerics.Vector4;

namespace FancyLighting.LightingEngines;

internal sealed class UltraFancyLightingEngine : FancyLightingEngineBase
{
    private readonly record struct LightSpread(
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

    private readonly LightSpread[] _lightSpread;

    private Vector3[] _tmp;
    private bool[] _skipGI;

    private bool _countTemporal;

    public UltraFancyLightingEngine()
    {
        ComputeLightSpread(out _lightSpread);
        InitializeDecayArrays();
        ComputeCircles();
    }

    private void ComputeLightSpread(out LightSpread[] values)
    {
        values = new LightSpread[(MAX_LIGHT_RANGE + 1) * (MAX_LIGHT_RANGE + 1)];
        DistanceCache[] distances = new DistanceCache[MAX_LIGHT_RANGE + 1];

        for (int row = 0; row <= MAX_LIGHT_RANGE; ++row)
        {
            int index = row;
            ref LightSpread value = ref values[index];
            value = CalculateTileLightSpread(row, 0, 0.0, 0.0);
            distances[row] = new(row + 1.0, row + value.DistanceToRight / (double)DISTANCE_TICKS);
        }

        for (int col = 1; col <= MAX_LIGHT_RANGE; ++col)
        {
            int index = (MAX_LIGHT_RANGE + 1) * col;
            ref LightSpread value = ref values[index];
            value = CalculateTileLightSpread(0, col, 0.0, 0.0);
            distances[0] = new(col + value.DistanceToTop / (double)DISTANCE_TICKS, col + 1.0);

            for (int row = 1; row <= MAX_LIGHT_RANGE; ++row)
            {
                ++index;
                double distance = MathUtil.Hypot(row, col);
                value = ref values[index];
                value = CalculateTileLightSpread(
                    row, col, distances[row].Right - distance, distances[row - 1].Top - distance
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

    private static LightSpread CalculateTileLightSpread(
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

        Span<double> lightFrom = stackalloc double[8 * 8];
        Span<double> area = stackalloc double[8];

        Span<double> x = stackalloc[] { 0.0, 0.0, 0.0, 0.0, 0.25, 0.5, 0.75, 1.0 };
        Span<double> y = stackalloc[] { 0.75, 0.5, 0.25, 0.0, 0.0, 0.0, 0.0, 0.0 };
        CalculateSubTileLightSpread(in x, in y, ref lightFrom, ref area, row, col);

        static double QuadrantSum(in Span<double> lightFrom, int index)
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

        static Vec4 VectorAt(in Span<double> lightFrom, int index)
            => new(
                (float)lightFrom[index],
                (float)lightFrom[index + 1],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 3]
            );

        static Vec4 ReverseVectorAt(in Span<double> lightFrom, int index)
            => new(
                (float)lightFrom[index + 3],
                (float)lightFrom[index + 2],
                (float)lightFrom[index + 1],
                (float)lightFrom[index]
            );

        distanceToTop
            -= QuadrantSum(lightFrom, 8 * 0 + 0) / 4.0 * leftDistanceError
            + QuadrantSum(lightFrom, 8 * 4 + 0) / 4.0 * bottomDistanceError;
        distanceToRight
            -= QuadrantSum(lightFrom, 8 * 0 + 4) / 4.0 * leftDistanceError
            + QuadrantSum(lightFrom, 8 * 4 + 4) / 4.0 * bottomDistanceError;

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
            VectorAt(lightFrom, 8 * 3 + 0),
            VectorAt(lightFrom, 8 * 2 + 0),
            VectorAt(lightFrom, 8 * 1 + 0),
            VectorAt(lightFrom, 8 * 0 + 0),
            VectorAt(lightFrom, 8 * 4 + 0),
            VectorAt(lightFrom, 8 * 5 + 0),
            VectorAt(lightFrom, 8 * 6 + 0),
            VectorAt(lightFrom, 8 * 7 + 0),
            ReverseVectorAt(lightFrom, 8 * 3 + 4),
            ReverseVectorAt(lightFrom, 8 * 2 + 4),
            ReverseVectorAt(lightFrom, 8 * 1 + 4),
            ReverseVectorAt(lightFrom, 8 * 0 + 4),
            ReverseVectorAt(lightFrom, 8 * 4 + 4),
            ReverseVectorAt(lightFrom, 8 * 5 + 4),
            ReverseVectorAt(lightFrom, 8 * 6 + 4),
            ReverseVectorAt(lightFrom, 8 * 7 + 4)
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
        UpdateDecays(lightMap);

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
        InitializeTaskVariables(length);

        bool doGI = LightingConfig.Instance.SimulateGlobalIllumination;

        _countTemporal = LightingConfig.Instance.FancyLightingEngineUseTemporal;
        RunLightingPass(
            colors,
            doGI ? _tmp : colors,
            length,
            _countTemporal,
            (Vec3[] lightMap, ref int temporalData, int begin, int end) =>
            {
                for (int i = begin; i < end; ++i)
                {
                    ProcessLight(lightMap, colors, ref temporalData, i, width, height);
                }
            }
        );

        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            if (_skipGI is null || _skipGI.Length < length)
            {
                _skipGI = new bool[length];
            }

            GetLightsForGlobalIllumination(
                _tmp, colors, colors, _skipGI, lightMasks, width, height
            );

            _countTemporal = false;
            RunLightingPass(
                _tmp,
                colors,
                length,
                false,
                (Vec3[] lightMap, ref int temporalData, int begin, int end) =>
                {
                    for (int i = begin; i < end; ++i)
                    {
                        if (_skipGI[i])
                        {
                            continue;
                        }

                        ProcessLight(lightMap, colors, ref temporalData, i, width, height);
                    }
                }
            );
        }
    }

    private void ProcessLight(
        Vec3[] lightMap, Vector3[] colors, ref int temporalData, int index, int width, int height
    )
    {
        ref Vector3 colorRef = ref colors[index];
        Vec3 color = new(colorRef.X, colorRef.Y, colorRef.Z);
        if (
            color.X <= _initialBrightnessCutoff
            && color.Y <= _initialBrightnessCutoff
            && color.Z <= _initialBrightnessCutoff
        )
        {
            return;
        }

        color *= _lightMask[index][DISTANCE_TICKS];

        CalculateLightSourceValues(
            colors,
            color,
            index,
            width,
            height,
            out int upDistance,
            out int downDistance,
            out int leftDistance,
            out int rightDistance,
            out bool doUp,
            out bool doDown,
            out bool doLeft,
            out bool doRight
        );

        // We blend by taking the max of each component, so this is a valid check to skip
        if (!(doUp || doDown || doLeft || doRight))
        {
            return;
        }

        int lightRange = CalculateLightRange(color);

        upDistance = Math.Min(upDistance, lightRange);
        downDistance = Math.Min(downDistance, lightRange);
        leftDistance = Math.Min(leftDistance, lightRange);
        rightDistance = Math.Min(rightDistance, lightRange);

        if (doUp)
        {
            SpreadLightLine(lightMap, color, index, upDistance, -1);
        }
        if (doDown)
        {
            SpreadLightLine(lightMap, color, index, downDistance, 1);
        }
        if (doLeft)
        {
            SpreadLightLine(lightMap, color, index, leftDistance, -height);
        }
        if (doRight)
        {
            SpreadLightLine(lightMap, color, index, rightDistance, height);
        }

        // Using && instead of || for culling is sometimes inaccurate, but much faster
        bool doUpperLeft = doUp && doLeft;
        bool doUpperRight = doUp && doRight;
        bool doLowerLeft = doDown && doLeft;
        bool doLowerRight = doDown && doRight;

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            int[] circle = _circles[lightRange];
            Span<Vec4> workingLights = stackalloc Vec4[lightRange + 1];

            if (doUpperLeft)
            {
                ProcessQuadrant(
                    lightMap, ref workingLights, circle, color, index, upDistance, leftDistance, -1, -height
                );
            }
            if (doUpperRight)
            {
                ProcessQuadrant(
                    lightMap, ref workingLights, circle, color, index, upDistance, rightDistance, -1, height
                );
            }
            if (doLowerLeft)
            {
                ProcessQuadrant(
                    lightMap, ref workingLights, circle, color, index, downDistance, leftDistance, 1, -height
                );
            }
            if (doLowerRight)
            {
                ProcessQuadrant(
                    lightMap, ref workingLights, circle, color, index, downDistance, rightDistance, 1, height
                );
            }
        }

        if (_countTemporal)
        {
            temporalData += CalculateTemporalData(
                color, doUp, doDown, doLeft, doRight, doUpperLeft, doUpperRight, doLowerLeft, doLowerRight
            );
        }
    }

    private void ProcessQuadrant(
        Vec3[] lightMap,
        ref Span<Vec4> workingLights,
        int[] circle,
        Vec3 color,
        int index,
        int verticalDistance,
        int horizontalDistance,
        int verticalChange,
        int horizontalChange
    )
    {
        // Performance optimization
        float[][] lightMask = _lightMask;
        float[] solidDecay = _lightSolidDecay;
        float lightLoss = _lightLossExitingSolid;
        LightSpread[] lightSpread = _lightSpread;

        {
            workingLights[0] = new(1f);
            int i = index + verticalChange;
            float value = 1f;
            float[] prevMask = lightMask[i];
            workingLights[1] = new(prevMask[lightSpread[1].DistanceToRight]);
            for (int y = 2; y <= verticalDistance; ++y)
            {
                i += verticalChange;

                float[] mask = lightMask[i];
                if (prevMask == solidDecay && mask != solidDecay)
                {
                    value *= lightLoss * prevMask[DISTANCE_TICKS];
                }
                else
                {
                    value *= prevMask[DISTANCE_TICKS];
                }
                prevMask = mask;

                workingLights[y] = new(value * mask[lightSpread[y].DistanceToRight]);
            }
        }

        for (int x = 1; x <= horizontalDistance; ++x)
        {
            int i = index + horizontalChange * x;
            int j = (MAX_LIGHT_RANGE + 1) * x;

            float[] mask = lightMask[i];

            Vec4 verticalLight;
            {
                ref Vec4 horizontalLight = ref workingLights[0];

                if (x > 1 && mask != solidDecay && lightMask[i - horizontalChange] == solidDecay)
                {
                    horizontalLight *= lightLoss;
                }

                verticalLight = horizontalLight * mask[lightSpread[j].DistanceToTop];
                horizontalLight *= mask[DISTANCE_TICKS];
            }

            int edge = Math.Min(verticalDistance, circle[x]);
            float[] prevMask = mask;
            for (int y = 1; y <= edge; ++y)
            {
                ref Vec4 horizontalLightRef = ref workingLights[y];
                Vec4 horizontalLight = horizontalLightRef;

                mask = lightMask[i += verticalChange];
                if (mask != solidDecay)
                {
                    if (prevMask == solidDecay)
                    {
                        verticalLight *= lightLoss;
                    }
                    if (lightMask[i - horizontalChange] == solidDecay)
                    {
                        horizontalLight *= lightLoss;
                    }
                }
                prevMask = mask;

                ref LightSpread spread = ref lightSpread[++j];

                SetLight(
                    ref lightMap[i],
                    (
                            Vec4.Dot(verticalLight, spread.LightFromBottom)
                            + Vec4.Dot(horizontalLight, spread.LightFromLeft)
                        ) * color
                );

                horizontalLightRef
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
}
