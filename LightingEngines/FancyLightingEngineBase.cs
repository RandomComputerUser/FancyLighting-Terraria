using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea; // Not used in the current version, but still nice to have
    private long _temporalData = 0;

    protected const int MAX_LIGHT_RANGE = 64;
    protected const int DISTANCE_TICKS = 256;

    private const float MAX_DECAY_MULT = 0.95f;
    private const float GI_MULT = 0.55f;
    private const float LOW_LIGHT_LEVEL = 0.03f;

    protected float _initialBrightnessCutoff;
    protected float _logBrightnessCutoff;
    protected float _logBasicWorkCutoff;

    protected float _thresholdMult;
    protected float _reciprocalLogSlowestDecay;
    protected float _lightLossExitingSolid;

    protected float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    protected float[] _lightWaterDecay;
    protected float[] _lightHoneyDecay;

    protected float[][] _lightMask;

    private Task[] _tasks;
    private Vec3[][] _workingLightMaps;
    private int[] _workingTemporalData;

    public void Unload() { }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    );

    protected delegate void LightingAction(
        Vec3[] workingLightMap,
        ref int workingTemporalData,
        int begin,
        int end
    );

    protected static void CalculateSubTileLightSpread(
        in Span<double> x,
        in Span<double> y,
        ref Span<double> lightFrom,
        ref Span<double> area,
        int row,
        int col
    )
    {
        var numSections = x.Length;
        var tMult = 0.5 * numSections;
        var leftX = col - 0.5;
        var rightX = col + 0.5;
        var bottomY = row - 0.5;
        var topY = row + 0.5;

        var previousT = 0.0;
        var index = 0;
        for (var i = 0; i < numSections; ++i)
        {
            var x1 = leftX + x[i];
            var y1 = bottomY + y[i];

            var slope = y1 / x1;

            double t;
            var x2 = rightX;
            var y2 = y1 + (x2 - x1) * slope;
            if (y2 > topY)
            {
                y2 = topY;
                x2 = x1 + (y2 - y1) / slope;
                t = tMult * (x2 - leftX);
            }
            else
            {
                t = tMult * ((topY - y2) + 1.0);
            }

            area[i] = (topY - y1) * (x2 - leftX) - 0.5 * (y2 - y1) * (x2 - x1);

            for (var j = 0; j < numSections; ++j)
            {
                if (j + 1 <= previousT)
                {
                    lightFrom[index++] = 0.0;
                    continue;
                }
                if (j >= t)
                {
                    lightFrom[index++] = 0.0;
                    continue;
                }

                var value = j < previousT ? j + 1 - previousT : 1.0;
                value -= j + 1 > t ? j + 1 - t : 0.0;
                lightFrom[index++] = value;
            }

            previousT = t;
        }
    }

    protected void InitializeDecayArrays()
    {
        _lightAirDecay = new float[DISTANCE_TICKS + 1];
        _lightSolidDecay = new float[DISTANCE_TICKS + 1];
        _lightWaterDecay = new float[DISTANCE_TICKS + 1];
        _lightHoneyDecay = new float[DISTANCE_TICKS + 1];
        for (var exponent = 0; exponent <= DISTANCE_TICKS; ++exponent)
        {
            _lightAirDecay[exponent] = 1f;
            _lightSolidDecay[exponent] = 1f;
            _lightWaterDecay[exponent] = 1f;
            _lightHoneyDecay[exponent] = 1f;
        }
    }

    protected void ComputeCircles()
    {
        _circles = new int[MAX_LIGHT_RANGE + 1][];
        _circles[0] = [0];
        for (var radius = 1; radius <= MAX_LIGHT_RANGE; ++radius)
        {
            _circles[radius] = new int[radius + 1];
            _circles[radius][0] = radius;
            var diagonal = radius / Math.Sqrt(2.0);
            for (var x = 1; x <= radius; ++x)
            {
                _circles[radius][x] =
                    x <= diagonal
                        ? (int)Math.Ceiling(Math.Sqrt(radius * radius - x * x))
                        : (int)Math.Floor(Math.Sqrt(radius * radius - (x - 1) * (x - 1)));
            }
        }
    }

    protected void UpdateBrightnessCutoff(
        float baseCutoff = 0.04f,
        float cameraModeCutoff = 0.02f,
        double temporalDataDivisor = 55555.5,
        double temporalMult = 0.02,
        double temporalMin = 0.02,
        double temporalMax = 0.125
    )
    {
        _initialBrightnessCutoff = LOW_LIGHT_LEVEL;

        var cutoff =
            FancyLightingMod._inCameraMode ? cameraModeCutoff
            : LightingConfig.Instance.FancyLightingEngineUseTemporal
                ? (float)
                    Math.Clamp(
                        Math.Sqrt(_temporalData / temporalDataDivisor) * temporalMult,
                        temporalMin,
                        temporalMax
                    )
            : baseCutoff;

        var basicWorkCutoff = baseCutoff;

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            GammaConverter.SrgbToLinear(ref _initialBrightnessCutoff);

            GammaConverter.GammaToLinear(ref cutoff);
            cutoff *= 1.25f; // Gamma is darker than sRGB for dark colors

            GammaConverter.SrgbToLinear(ref basicWorkCutoff);
        }

        _logBrightnessCutoff = MathF.Log(cutoff);
        _logBasicWorkCutoff = MathF.Log(basicWorkCutoff);
    }

    protected void UpdateDecays(LightMap lightMap)
    {
        var decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter
            ? 1f
            : 0.975f;
        float lightAirDecayBaseline =
            decayMult * Math.Min(lightMap.LightDecayThroughAir, MAX_DECAY_MULT);
        float lightSolidDecayBaseline =
            decayMult
            * Math.Min(
                MathF.Pow(
                    lightMap.LightDecayThroughSolid,
                    LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
                ),
                MAX_DECAY_MULT
            );
        float lightWaterDecayBaseline =
            decayMult
            * Math.Min(
                0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
                    + 0.375f
                        * Math.Max(
                            lightMap.LightDecayThroughWater.X,
                            Math.Max(
                                lightMap.LightDecayThroughWater.Y,
                                lightMap.LightDecayThroughWater.Z
                            )
                        ),
                MAX_DECAY_MULT
            );
        float lightHoneyDecayBaseline =
            decayMult
            * Math.Min(
                0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
                    + 0.375f
                        * Math.Max(
                            lightMap.LightDecayThroughHoney.X,
                            Math.Max(
                                lightMap.LightDecayThroughHoney.Y,
                                lightMap.LightDecayThroughHoney.Z
                            )
                        ),
                MAX_DECAY_MULT
            );

        _lightLossExitingSolid =
            LightingConfig.Instance.FancyLightingEngineExitMultiplier();

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            GammaConverter.GammaToLinear(ref lightAirDecayBaseline);
            GammaConverter.GammaToLinear(ref lightSolidDecayBaseline);
            GammaConverter.GammaToLinear(ref lightWaterDecayBaseline);
            GammaConverter.GammaToLinear(ref lightHoneyDecayBaseline);

            GammaConverter.GammaToLinear(ref _lightLossExitingSolid);
        }

        const float THRESHOLD_MULT_EXPONENT = 0.41421354f; // sqrt(2) - 1

        var logSlowestDecay = MathF.Log(
            Math.Max(
                Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
                Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
            )
        );
        _thresholdMult = MathF.Exp(THRESHOLD_MULT_EXPONENT * logSlowestDecay);
        _reciprocalLogSlowestDecay = 1f / logSlowestDecay;

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline);
    }

    private static void UpdateDecay(float[] decay, float baseline)
    {
        if (baseline == decay[DISTANCE_TICKS])
        {
            return;
        }

        var logBaseline = MathF.Log(baseline);
        var exponentMult = 1f / DISTANCE_TICKS;
        for (var i = 0; i < decay.Length; ++i)
        {
            decay[i] = MathF.Exp(exponentMult * i * logBaseline);
        }
    }

    protected void UpdateLightMasks(LightMaskMode[] lightMasks, int width, int height) =>
        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount,
            },
            (i) =>
            {
                var endIndex = height * (i + 1);
                for (var j = height * i; j < endIndex; ++j)
                {
                    _lightMask[j] = lightMasks[j] switch
                    {
                        LightMaskMode.Solid => _lightSolidDecay,
                        LightMaskMode.Water => _lightWaterDecay,
                        LightMaskMode.Honey => _lightHoneyDecay,
                        _ => _lightAirDecay,
                    };
                }
            }
        );

    protected static void ConvertLightColorsToLinear(
        Vector3[] colors,
        int width,
        int height
    ) =>
        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount,
            },
            (x) =>
            {
                var i = height * x;
                for (var y = 0; y < height; ++y)
                {
                    GammaConverter.SrgbToLinear(ref colors[i++]);
                }
            }
        );

    protected void InitializeTaskVariables(int lightMapSize)
    {
        var taskCount = LightingConfig.Instance.ThreadCount;

        if (_tasks is null)
        {
            _tasks = new Task[taskCount];
            _workingLightMaps = new Vec3[taskCount][];
            _workingTemporalData = new int[taskCount];

            for (var i = 0; i < taskCount; ++i)
            {
                _workingLightMaps[i] = new Vec3[lightMapSize];
            }
        }
        else if (_tasks.Length != taskCount)
        {
            _tasks = new Task[taskCount];
            _workingTemporalData = new int[taskCount];

            var workingLightMaps = new Vec3[taskCount][];
            var numToCopy = Math.Min(_workingLightMaps.Length, taskCount);

            for (var i = 0; i < numToCopy; ++i)
            {
                if (_workingLightMaps[i].Length >= lightMapSize)
                {
                    workingLightMaps[i] = _workingLightMaps[i];
                }
                else
                {
                    workingLightMaps[i] = new Vec3[lightMapSize];
                }
            }

            for (var i = numToCopy; i < taskCount; ++i)
            {
                workingLightMaps[i] = new Vec3[lightMapSize];
            }

            _workingLightMaps = workingLightMaps;
        }
        else
        {
            for (var i = 0; i < taskCount; ++i)
            {
                ArrayUtil.MakeAtLeastSize(ref _workingLightMaps[i], lightMapSize);
            }
        }
    }

    protected void RunLightingPass(
        Vector3[] initialLightMapValue,
        Vector3[] destination,
        int lightMapSize,
        bool countTemporalData,
        LightingAction lightingAction
    )
    {
        var taskCount = LightingConfig.Instance.ThreadCount;

        if (countTemporalData)
        {
            for (var i = 0; i < taskCount; ++i)
            {
                _workingTemporalData[i] = 0;
            }
        }

        if (taskCount <= 1)
        {
            var workingLightMap = _workingLightMaps[0];
            ref var workingTemporalData = ref _workingTemporalData[0];

            CopyVec3Array(initialLightMapValue, workingLightMap, 0, lightMapSize);

            lightingAction(workingLightMap, ref workingTemporalData, 0, lightMapSize);

            CopyVec3Array(workingLightMap, destination, 0, lightMapSize);
            _temporalData = workingTemporalData;

            return;
        }

        const int INDEX_INCREMENT = 32;

        var taskIndex = -1;
        var lightIndex = -INDEX_INCREMENT;
        for (var i = 0; i < taskCount; ++i)
        {
            _tasks[i] = Task.Factory.StartNew(() =>
            {
                var index = Interlocked.Increment(ref taskIndex);

                var workingLightMap = _workingLightMaps[index];
                ref var workingTemporalData = ref _workingTemporalData[index];

                CopyVec3Array(initialLightMapValue, workingLightMap, 0, lightMapSize);

                while (true)
                {
                    var i = Interlocked.Add(ref lightIndex, INDEX_INCREMENT);
                    if (i >= lightMapSize)
                    {
                        break;
                    }

                    lightingAction(
                        workingLightMap,
                        ref workingTemporalData,
                        i,
                        Math.Min(lightMapSize, i + INDEX_INCREMENT)
                    );
                }
            });
        }

        Task.WaitAll(_tasks);

        const int CHUNK_SIZE = 64;

        Parallel.For(
            0,
            (lightMapSize - 1) / CHUNK_SIZE + 1,
            new ParallelOptions { MaxDegreeOfParallelism = taskCount },
            (i) =>
            {
                var begin = CHUNK_SIZE * i;
                var end = Math.Min(lightMapSize, begin + CHUNK_SIZE);

                for (var j = 1; j < _workingLightMaps.Length; ++j)
                {
                    MaxArraysIntoFirst(
                        _workingLightMaps[0],
                        _workingLightMaps[j],
                        begin,
                        end
                    );
                }

                CopyVec3Array(_workingLightMaps[0], destination, begin, end);
            }
        );

        if (countTemporalData)
        {
            _temporalData = 0;
            for (var i = 0; i < taskCount; ++i)
            {
                _temporalData += _workingTemporalData[i];
            }
        }
    }

    private static void MaxArraysIntoFirst(Vec3[] arr1, Vec3[] arr2, int begin, int end)
    {
        for (var i = begin; i < end; ++i)
        {
            ref var value = ref arr1[i];
            value = Vec3.Max(value, arr2[i]);
        }
    }

    private static void CopyVec3Array(
        Vector3[] source,
        Vec3[] destination,
        int begin,
        int end
    )
    {
        for (var i = begin; i < end; ++i)
        {
            ref var sourceValue = ref source[i];
            ref var destinationValue = ref destination[i];
            destinationValue.X = sourceValue.X;
            destinationValue.Y = sourceValue.Y;
            destinationValue.Z = sourceValue.Z;
        }
    }

    private static void CopyVec3Array(
        Vec3[] source,
        Vector3[] destination,
        int begin,
        int end
    )
    {
        for (var i = begin; i < end; ++i)
        {
            ref var sourceValue = ref source[i];
            ref var destinationValue = ref destination[i];
            destinationValue.X = sourceValue.X;
            destinationValue.Y = sourceValue.Y;
            destinationValue.Z = sourceValue.Z;
        }
    }

    protected static void GetLightsForGlobalIllumination(
        Vector3[] source,
        Vector3[] destination,
        Vector3[] lightSources,
        bool[] skipGI,
        LightMaskMode[] lightMasks,
        int width,
        int height
    )
    {
        var giMult = GI_MULT;
        if (LightingConfig.Instance.DoGammaCorrection())
        {
            // Gamma correction darkens dark colors, so this helps compensate
            giMult *= 1.1f;

            GammaConverter.GammaToLinear(ref giMult);
        }

        Parallel.For(
            0,
            width,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount,
            },
            (i) =>
            {
                var endIndex = height * (i + 1);
                for (var j = height * i; j < endIndex; ++j)
                {
                    ref var giLight = ref destination[j];

                    if (lightMasks[j] is LightMaskMode.Solid)
                    {
                        giLight.X = 0f;
                        giLight.Y = 0f;
                        giLight.Z = 0f;
                        skipGI[j] = true;
                        continue;
                    }

                    var origLight = lightSources[j];
                    ref var light = ref source[j];
                    giLight.X = giMult * light.X;
                    giLight.Y = giMult * light.Y;
                    giLight.Z = giMult * light.Z;

                    skipGI[j] =
                        giLight.X <= origLight.X
                        && giLight.Y <= origLight.Y
                        && giLight.Z <= origLight.Z;
                }
            }
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CalculateLightSourceValues(
        Vector3[] colors,
        Vec3 color,
        int index,
        int width,
        int height,
        out int upDistance,
        out int downDistance,
        out int leftDistance,
        out int rightDistance,
        out bool doUp,
        out bool doDown,
        out bool doLeft,
        out bool doRight
    )
    {
        (var x, var y) = Math.DivRem(index, height);

        upDistance = y;
        downDistance = height - 1 - y;
        leftDistance = x;
        rightDistance = width - 1 - x;

        var threshold = _thresholdMult * color;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThanThreshold(int otherIndex)
        {
            ref var otherColorRef = ref colors[otherIndex];
            Vec3 otherColor = new(otherColorRef.X, otherColorRef.Y, otherColorRef.Z);
            otherColor *= _lightMask[otherIndex][DISTANCE_TICKS];
            return otherColor.X < threshold.X
                || otherColor.Y < threshold.Y
                || otherColor.Z < threshold.Z;
        }

        doUp = upDistance > 0 ? LessThanThreshold(index - 1) : false;
        doDown = downDistance > 0 ? LessThanThreshold(index + 1) : false;
        doLeft = leftDistance > 0 ? LessThanThreshold(index - height) : false;
        doRight = rightDistance > 0 ? LessThanThreshold(index + height) : false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int CalculateLightRange(Vec3 color) =>
        Math.Clamp(
            (int)
                Math.Ceiling(
                    (
                        _logBrightnessCutoff
                        - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) * _reciprocalLogSlowestDecay
                ) + 1,
            1,
            MAX_LIGHT_RANGE
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void SetLight(ref Vec3 light, Vec3 value) =>
        light = Vec3.Max(light, value);

    protected void SpreadLightLine(
        Vec3[] lightMap,
        Vec3 color,
        int index,
        int distance,
        int indexChange
    )
    {
        // Performance optimization
        var lightMask = _lightMask;
        var solidDecay = _lightSolidDecay;
        var lightLoss = _lightLossExitingSolid;

        index += indexChange;
        SetLight(ref lightMap[index], color);

        // Would multiply by (distance + 1), but we already incremented index once
        var endIndex = index + distance * indexChange;
        var prevMask = lightMask[index];
        while (true)
        {
            index += indexChange;
            if (index == endIndex)
            {
                break;
            }

            var mask = lightMask[index];
            if (prevMask == solidDecay && mask != solidDecay)
            {
                color *= lightLoss * prevMask[DISTANCE_TICKS];
            }
            else
            {
                color *= prevMask[DISTANCE_TICKS];
            }
            prevMask = mask;

            SetLight(ref lightMap[index], color);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int CalculateTemporalData(
        Vec3 color,
        bool doUp,
        bool doDown,
        bool doLeft,
        bool doRight,
        bool doUpperLeft,
        bool doUpperRight,
        bool doLowerLeft,
        bool doLowerRight
    )
    {
        var baseWork = Math.Clamp(
            (int)
                Math.Ceiling(
                    (
                        _logBasicWorkCutoff
                        - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                    ) * _reciprocalLogSlowestDecay
                ) + 1,
            1,
            MAX_LIGHT_RANGE
        );

        var approximateWorkDone =
            1
            + ((doUp ? 1 : 0) + (doDown ? 1 : 0) + (doLeft ? 1 : 0) + (doRight ? 1 : 0))
                * baseWork
            + (
                (doUpperLeft ? 1 : 0)
                + (doUpperRight ? 1 : 0)
                + (doLowerLeft ? 1 : 0)
                + (doLowerRight ? 1 : 0)
            ) * (baseWork * baseWork);

        return approximateWorkDone;
    }
}
