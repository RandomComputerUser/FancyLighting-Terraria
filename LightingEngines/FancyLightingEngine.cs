using System;
using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal sealed class FancyLightingEngine : FancyLightingEngineBase
{
    private readonly record struct LightSpread(
        int DistanceToTop,
        int DistanceToRight,
        float LightFromLeft,
        float LightFromBottom,
        float TopFromLeft,
        float TopFromBottom,
        float RightFromLeft,
        float RightFromBottom
    );

    private readonly record struct DistanceCache(double Top, double Right);

    private readonly LightSpread[] _lightSpread;

    private Vector3[] _tmp;
    private bool[] _skipGI;

    private bool _countTemporal;

    public FancyLightingEngine()
    {
        ComputeLightSpread(out _lightSpread);
        InitializeDecayArrays();
        ComputeCircles();
    }

    private void ComputeLightSpread(out LightSpread[] values)
    {
        values = new LightSpread[(MAX_LIGHT_RANGE + 1) * (MAX_LIGHT_RANGE + 1)];
        var distances = new DistanceCache[MAX_LIGHT_RANGE + 1];

        for (var row = 0; row <= MAX_LIGHT_RANGE; ++row)
        {
            var index = row;
            ref var value = ref values[index];
            value = CalculateTileLightSpread(row, 0, 0.0, 0.0);
            distances[row] = new(
                row + 1.0,
                row + value.DistanceToRight / (double)DISTANCE_TICKS
            );
        }

        for (var col = 1; col <= MAX_LIGHT_RANGE; ++col)
        {
            var index = (MAX_LIGHT_RANGE + 1) * col;
            ref var value = ref values[index];
            value = CalculateTileLightSpread(0, col, 0.0, 0.0);
            distances[0] = new(
                col + value.DistanceToTop / (double)DISTANCE_TICKS,
                col + 1.0
            );

            for (var row = 1; row <= MAX_LIGHT_RANGE; ++row)
            {
                ++index;
                var distance = MathUtil.Hypot(col, row);
                value = ref values[index];
                value = CalculateTileLightSpread(
                    row,
                    col,
                    distances[row].Right - distance,
                    distances[row - 1].Top - distance
                );

                distances[row] = new(
                    value.DistanceToTop / (double)DISTANCE_TICKS
                        + value.TopFromLeft * distances[row].Right
                        + value.TopFromBottom * distances[row - 1].Top,
                    value.DistanceToRight / (double)DISTANCE_TICKS
                        + value.RightFromLeft * distances[row].Right
                        + value.RightFromBottom * distances[row - 1].Top
                );
            }
        }
    }

    private static LightSpread CalculateTileLightSpread(
        int row,
        int col,
        double leftDistanceError,
        double bottomDistanceError
    )
    {
        static int DoubleToIndex(double x) =>
            Math.Clamp((int)Math.Round(DISTANCE_TICKS * x), 0, DISTANCE_TICKS);

        var distance = MathUtil.Hypot(col, row);
        var distanceToTop = MathUtil.Hypot(col, row + 1) - distance;
        var distanceToRight = MathUtil.Hypot(col + 1, row) - distance;

        if (row == 0 && col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                0f,
                0f,
                0f,
                0f,
                0f,
                0f
            );
        }

        if (row == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                0f,
                0f,
                0f,
                0f,
                0f,
                0f
            );
        }

        if (col == 0)
        {
            return new(
                DoubleToIndex(distanceToTop),
                DoubleToIndex(distanceToRight),
                // The values below are unused and should never be used
                0f,
                0f,
                0f,
                0f,
                0f,
                0f
            );
        }

        Span<double> lightFrom = stackalloc double[2 * 2];
        Span<double> area = stackalloc double[2];

        var x = stackalloc[] { 0.0, 1.0 };
        var y = stackalloc[] { 0.0, 0.0 };
        CalculateSubTileLightSpread(in x, in y, ref lightFrom, ref area, row, col);

        distanceToTop -=
            lightFrom[0] * leftDistanceError + lightFrom[2] * bottomDistanceError;
        distanceToRight -=
            lightFrom[1] * leftDistanceError + lightFrom[3] * bottomDistanceError;

        return new(
            DoubleToIndex(distanceToTop),
            DoubleToIndex(distanceToRight),
            (float)area[0],
            (float)(area[1] - area[0]),
            (float)lightFrom[0],
            (float)lightFrom[2],
            (float)lightFrom[1],
            (float)lightFrom[3]
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

        var length = width * height;

        ArrayUtil.MakeAtLeastSize(ref _tmp, length);
        ArrayUtil.MakeAtLeastSize(ref _lightMask, length);

        Array.Copy(colors, _tmp, length);
        UpdateLightMasks(lightMasks, width, height);
        InitializeTaskVariables(length);

        var doGI = LightingConfig.Instance.SimulateGlobalIllumination;

        _countTemporal = LightingConfig.Instance.FancyLightingEngineUseTemporal;
        RunLightingPass(
            colors,
            doGI ? _tmp : colors,
            length,
            _countTemporal,
            (Vec3[] lightMap, ref int temporalData, int begin, int end) =>
            {
                for (var i = begin; i < end; ++i)
                {
                    ProcessLight(lightMap, colors, ref temporalData, i, width, height);
                }
            }
        );

        if (LightingConfig.Instance.SimulateGlobalIllumination)
        {
            ArrayUtil.MakeAtLeastSize(ref _skipGI, length);

            GetLightsForGlobalIllumination(
                _tmp,
                colors,
                colors,
                _skipGI,
                lightMasks,
                width,
                height
            );

            _countTemporal = false;
            RunLightingPass(
                _tmp,
                colors,
                length,
                false,
                (Vec3[] lightMap, ref int temporalData, int begin, int end) =>
                {
                    for (var i = begin; i < end; ++i)
                    {
                        if (_skipGI[i])
                        {
                            continue;
                        }

                        ProcessLight(
                            lightMap,
                            colors,
                            ref temporalData,
                            i,
                            width,
                            height
                        );
                    }
                }
            );
        }
    }

    private void ProcessLight(
        Vec3[] lightMap,
        Vector3[] colors,
        ref int temporalData,
        int index,
        int width,
        int height
    )
    {
        ref var colorRef = ref colors[index];
        var color = new Vec3(colorRef.X, colorRef.Y, colorRef.Z);
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
            out var upDistance,
            out var downDistance,
            out var leftDistance,
            out var rightDistance,
            out var doUp,
            out var doDown,
            out var doLeft,
            out var doRight
        );

        // We blend by taking the max of each component, so this is a valid check to skip
        if (!(doUp || doDown || doLeft || doRight))
        {
            return;
        }

        var lightRange = CalculateLightRange(color);

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
        var doUpperLeft = doUp && doLeft;
        var doUpperRight = doUp && doRight;
        var doLowerLeft = doDown && doLeft;
        var doLowerRight = doDown && doRight;

        if (doUpperRight || doUpperLeft || doLowerRight || doLowerLeft)
        {
            var circle = _circles[lightRange];
            Span<float> workingLights = stackalloc float[lightRange + 1];

            if (doUpperLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    ref workingLights,
                    circle,
                    color,
                    index,
                    upDistance,
                    leftDistance,
                    -1,
                    -height
                );
            }
            if (doUpperRight)
            {
                ProcessQuadrant(
                    lightMap,
                    ref workingLights,
                    circle,
                    color,
                    index,
                    upDistance,
                    rightDistance,
                    -1,
                    height
                );
            }
            if (doLowerLeft)
            {
                ProcessQuadrant(
                    lightMap,
                    ref workingLights,
                    circle,
                    color,
                    index,
                    downDistance,
                    leftDistance,
                    1,
                    -height
                );
            }
            if (doLowerRight)
            {
                ProcessQuadrant(
                    lightMap,
                    ref workingLights,
                    circle,
                    color,
                    index,
                    downDistance,
                    rightDistance,
                    1,
                    height
                );
            }
        }

        if (_countTemporal)
        {
            temporalData += CalculateTemporalData(
                color,
                doUp,
                doDown,
                doLeft,
                doRight,
                doUpperLeft,
                doUpperRight,
                doLowerLeft,
                doLowerRight
            );
        }
    }

    private void ProcessQuadrant(
        Vec3[] lightMap,
        ref Span<float> workingLights,
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
        var lightMask = _lightMask;
        var solidDecay = _lightSolidDecay;
        var lightLoss = _lightLossExitingSolid;
        var lightSpread = _lightSpread;

        {
            workingLights[0] = 1f;
            var i = index + verticalChange;
            var value = 1f;
            var prevMask = lightMask[i];
            workingLights[1] = prevMask[lightSpread[1].DistanceToRight];
            for (var y = 2; y <= verticalDistance; ++y)
            {
                i += verticalChange;

                var mask = lightMask[i];
                if (prevMask == solidDecay && mask != solidDecay)
                {
                    value *= lightLoss * prevMask[DISTANCE_TICKS];
                }
                else
                {
                    value *= prevMask[DISTANCE_TICKS];
                }
                prevMask = mask;

                workingLights[y] = value * mask[lightSpread[y].DistanceToRight];
            }
        }

        for (var x = 1; x <= horizontalDistance; ++x)
        {
            var i = index + horizontalChange * x;
            var j = (MAX_LIGHT_RANGE + 1) * x;

            var mask = lightMask[i];

            float verticalLight;
            {
                ref var horizontalLight = ref workingLights[0];

                if (
                    x > 1
                    && mask != solidDecay
                    && lightMask[i - horizontalChange] == solidDecay
                )
                {
                    horizontalLight *= lightLoss;
                }

                verticalLight = horizontalLight * mask[lightSpread[j].DistanceToTop];
                horizontalLight *= mask[DISTANCE_TICKS];
            }

            var edge = Math.Min(verticalDistance, circle[x]);
            var prevMask = mask;
            for (var y = 1; y <= edge; ++y)
            {
                ref var horizontalLightRef = ref workingLights[y];
                var horizontalLight = horizontalLightRef;

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

                ref var spread = ref lightSpread[++j];

                SetLight(
                    ref lightMap[i],
                    (
                        spread.LightFromBottom * verticalLight
                        + spread.LightFromLeft * horizontalLight
                    ) * color
                );

                horizontalLightRef =
                    (
                        spread.RightFromBottom * verticalLight
                        + spread.RightFromLeft * horizontalLight
                    ) * mask[spread.DistanceToRight];
                verticalLight =
                    (
                        spread.TopFromLeft * horizontalLight
                        + spread.TopFromBottom * verticalLight
                    ) * mask[spread.DistanceToTop];
            }
        }
    }
}
