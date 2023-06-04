using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;
using Vec3 = System.Numerics.Vector3;

namespace FancyLighting.LightingEngines;

internal abstract class FancyLightingEngineBase : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;
    protected int _temporalData;

    protected const int MAX_LIGHT_RANGE = 64;
    protected const int DISTANCE_TICKS = 256;

    private const float MAX_DECAY_MULT = 0.95f;
    private const float GI_MULT = 0.55f;
    private const float LOW_LIGHT_LEVEL = 0.03f;

    protected float _initialBrightnessCutoff;
    protected float _thresholdMult;
    protected float _logBrightnessCutoff;
    protected float _reciprocalLogSlowestDecay;
    protected float _lightLossExitingSolid;

    protected float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    protected float[] _lightWaterDecay;
    protected float[] _lightHoneyDecay;
    protected float[] _lightShadowPaintDecay; // In vanilla, shadow paint isn't a special case

    protected float[][] _lightMask;

    private Task[] _tasks;
    private Vector3[][] _workingLightMaps;

    public void Unload()
    { }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
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
        int numSections = x.Length;
        double tMult = 0.5 * numSections;
        double leftX = col - 0.5;
        double rightX = col + 0.5;
        double bottomY = row - 0.5;
        double topY = row + 0.5;

        double previousT = 0.0;
        int index = 0;
        for (int i = 0; i < numSections; ++i)
        {
            double x1 = leftX + x[i];
            double y1 = bottomY + y[i];

            double slope = y1 / x1;

            double t;
            double x2 = rightX;
            double y2 = y1 + (x2 - x1) * slope;
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

            for (int j = 0; j < numSections; ++j)
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

                double value = j < previousT ? j + 1 - previousT : 1.0;
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
        _lightShadowPaintDecay = new float[DISTANCE_TICKS + 1];
        for (int exponent = 0; exponent <= DISTANCE_TICKS; ++exponent)
        {
            _lightAirDecay[exponent] = 1f;
            _lightSolidDecay[exponent] = 1f;
            _lightWaterDecay[exponent] = 1f;
            _lightHoneyDecay[exponent] = 1f;
            _lightShadowPaintDecay[exponent] = 0f;
        }
    }

    protected void ComputeCircles()
    {
        _circles = new int[MAX_LIGHT_RANGE + 1][];
        _circles[0] = new int[] { 0 };
        for (int radius = 1; radius <= MAX_LIGHT_RANGE; ++radius)
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

        float cutoff = FancyLightingMod._inCameraMode
            ? cameraModeCutoff
            : LightingConfig.Instance.FancyLightingEngineUseTemporal
                ? (float)Math.Clamp(
                        Math.Sqrt(_temporalData / temporalDataDivisor) * temporalMult,
                        temporalMin,
                        temporalMax
                    )
                : baseCutoff;

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            _initialBrightnessCutoff *= _initialBrightnessCutoff;
            cutoff *= cutoff;
        }

        _logBrightnessCutoff = MathF.Log(cutoff);
        _temporalData = 0;
    }

    protected void UpdateDecays(LightMap lightMap)
    {
        float decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
        float lightAirDecayBaseline
            = decayMult * Math.Min(lightMap.LightDecayThroughAir, MAX_DECAY_MULT);
        float lightSolidDecayBaseline = decayMult * Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            MAX_DECAY_MULT
        );
        float lightWaterDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughWater.X,
                Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
            ),
            MAX_DECAY_MULT
        );
        float lightHoneyDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughHoney.X,
                Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
            ),
            MAX_DECAY_MULT
        );

        _lightLossExitingSolid = LightingConfig.Instance.FancyLightingEngineExitMultiplier();

        if (LightingConfig.Instance.DoGammaCorrection())
        {
            lightAirDecayBaseline *= lightAirDecayBaseline;
            lightSolidDecayBaseline *= lightSolidDecayBaseline;
            lightWaterDecayBaseline *= lightWaterDecayBaseline;
            lightHoneyDecayBaseline *= lightHoneyDecayBaseline;

            _lightLossExitingSolid *= _lightLossExitingSolid;
        }

        const float THRESHOLD_MULT_EXPONENT = 0.41421354f; // sqrt(2) - 1

        float logSlowestDecay = MathF.Log(Math.Max(
            Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
            Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
        ));
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

        float logBaseline = MathF.Log(baseline);
        float exponentMult = 1f / DISTANCE_TICKS;
        for (int i = 0; i < decay.Length; ++i)
        {
            decay[i] = MathF.Exp(exponentMult * i * logBaseline);
        }
    }

    protected void UpdateLightMasks(
        LightMaskMode[] lightMasks, int width, int height
    ) => Parallel.For(
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
                _lightMask[j] = lightMasks[j] switch
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

    protected static void ConvertLightColorsToLinear(Vector3[] colors, int width, int height)
        => Parallel.For(
            0,
            width,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (x) =>
            {
                int i = height * x;
                for (int y = 0; y < height; ++y)
                {
                    GammaConverter.GammaToLinear(ref colors[i++]);
                }
            }
        );

    protected void InitializeTaskVariables(int lightMapSize)
    {
        int taskCount = LightingConfig.Instance.ThreadCount;

        if (_tasks is null)
        {
            _tasks = new Task[taskCount];
            _workingLightMaps = new Vector3[taskCount][];

            for (int i = 0; i < taskCount; ++i)
            {
                _workingLightMaps[i] = new Vector3[lightMapSize];
            }
        }
        else if (_tasks.Length != taskCount)
        {
            _tasks = new Task[taskCount];

            Vector3[][] workingLightMaps = new Vector3[taskCount][];
            int numToCopy = Math.Min(_workingLightMaps.Length, taskCount);

            for (int i = 0; i < numToCopy; ++i)
            {
                if (_workingLightMaps[i].Length >= lightMapSize)
                {
                    workingLightMaps[i] = _workingLightMaps[i];
                }
                else
                {
                    workingLightMaps[i] = new Vector3[lightMapSize];
                }
            }

            for (int i = numToCopy; i < taskCount; ++i)
            {
                workingLightMaps[i] = new Vector3[lightMapSize];
            }

            _workingLightMaps = workingLightMaps;
        }
        else
        {
            for (int i = 0; i < taskCount; ++i)
            {
                if (_workingLightMaps[i].Length < lightMapSize)
                {
                    _workingLightMaps[i] = new Vector3[lightMapSize];
                }
            }
        }
    }

    protected void RunLightingPass(
        Vector3[] initialLightMapValue,
        Vector3[] destination,
        int lightMapSize,
        Action<Vector3[], int> lightingAction
    )
    {
        int taskCount = LightingConfig.Instance.ThreadCount;

        if (taskCount <= 1)
        {
            Vector3[] workingLightMap = _workingLightMaps[0];

            Array.Copy(initialLightMapValue, workingLightMap, lightMapSize);

            for (int i = 0; i < lightMapSize; ++i)
            {
                lightingAction(workingLightMap, i);
            }

            Array.Copy(workingLightMap, destination, lightMapSize);

            return;
        }

        const int INDEX_INCREMENT = 32;

        int taskIndex = -1;
        int lightIndex = -INDEX_INCREMENT;
        for (int i = 0; i < taskCount; ++i)
        {
            _tasks[i] = Task.Factory.StartNew(
                () =>
                {
                    int index = Interlocked.Increment(ref taskIndex);

                    Vector3[] workingLightMap = _workingLightMaps[index];

                    Array.Copy(initialLightMapValue, workingLightMap, lightMapSize);

                    while (true)
                    {
                        int i = Interlocked.Add(ref lightIndex, INDEX_INCREMENT);
                        if (i >= lightMapSize)
                        {
                            break;
                        }

                        for (int end = Math.Min(lightMapSize, i + INDEX_INCREMENT); i < end; ++i)
                        {
                            lightingAction(workingLightMap, i);
                        }
                    }
                }
            );
        }

        Task.WaitAll(_tasks);

        static void MaxArraysIntoFirst(Vector3[] arr1, Vector3[] arr2, int begin, int end)
        {
            for (int i = begin; i < end; ++i)
            {
                ref Vector3 vec1 = ref arr1[i];
                ref Vector3 vec2 = ref arr2[i];

                if (vec2.X > vec1.X)
                {
                    vec1.X = vec2.X;
                }
                if (vec2.Y > vec1.Y)
                {
                    vec1.Y = vec2.Y;
                }
                if (vec2.Z > vec1.Z)
                {
                    vec1.Z = vec2.Z;
                }
            }
        }

        static void MaxArrays(Vector3[] arr1, Vector3[] arr2, Vector3[] result, int begin, int end)
        {
            for (int i = begin; i < end; ++i)
            {
                ref Vector3 vec1 = ref arr1[i];
                ref Vector3 vec2 = ref arr2[i];
                ref Vector3 resultVec = ref result[i];

                resultVec.X = vec2.X > vec1.X ? vec2.X : vec1.X;
                resultVec.Y = vec2.Y > vec1.Y ? vec2.Y : vec1.Y;
                resultVec.Z = vec2.Z > vec1.Z ? vec2.Z : vec1.Z;
            }
        }

        const int CHUNK_SIZE = 64;

        Parallel.For(
            0,
            (lightMapSize - 1) / CHUNK_SIZE + 1,
            new ParallelOptions { MaxDegreeOfParallelism = taskCount },
            (i) =>
            {
                int begin = CHUNK_SIZE * i;
                int end = Math.Min(lightMapSize, begin + CHUNK_SIZE);

                MaxArrays(_workingLightMaps[0], _workingLightMaps[1], destination, begin, end);
                for (int j = 2; j < _workingLightMaps.Length; ++j)
                {
                    MaxArraysIntoFirst(destination, _workingLightMaps[j], begin, end);
                }
            }
        );
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
        float giMult = GI_MULT;
        if (LightingConfig.Instance.DoGammaCorrection())
        {
            giMult *= giMult;
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
                    ref Vector3 giLight = ref destination[j];

                    if (lightMasks[j] is LightMaskMode.Solid)
                    {
                        giLight.X = 0f;
                        giLight.Y = 0f;
                        giLight.Z = 0f;
                        skipGI[j] = true;
                        continue;
                    }

                    Vector3 origLight = lightSources[j];
                    ref Vector3 light = ref source[j];
                    giLight.X = giMult * light.X;
                    giLight.Y = giMult * light.Y;
                    giLight.Z = giMult * light.Z;

                    skipGI[j]
                        = giLight.X <= origLight.X
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
        (int x, int y) = Math.DivRem(index, height);

        upDistance = y;
        downDistance = height - 1 - y;
        leftDistance = x;
        rightDistance = width - 1 - x;

        Vec3 threshold = _thresholdMult * color;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool LessThanThreshold(int otherIndex)
        {
            ref Vector3 otherColorRef = ref colors[otherIndex];
            Vec3 otherColor = new(otherColorRef.X, otherColorRef.Y, otherColorRef.Z);
            otherColor *= _lightMask[otherIndex][DISTANCE_TICKS];
            return otherColor.X < threshold.X || otherColor.Y < threshold.Y || otherColor.Z < threshold.Z;
        }

        doUp = upDistance > 0 ? LessThanThreshold(index - 1) : false;
        doDown = downDistance > 0 ? LessThanThreshold(index + 1) : false;
        doLeft = leftDistance > 0 ? LessThanThreshold(index - height) : false;
        doRight = rightDistance > 0 ? LessThanThreshold(index + height) : false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int CalculateLightRange(Vec3 color)
        => Math.Clamp(
            (int)Math.Ceiling(
                (
                    _logBrightnessCutoff
                    - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                ) * _reciprocalLogSlowestDecay
            ) + 1,
            1,
            MAX_LIGHT_RANGE
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void SetLight(ref Vector3 light, Vec3 value)
    {
        if (value.X > light.X)
        {
            light.X = value.X;
        }
        if (value.Y > light.Y)
        {
            light.Y = value.Y;
        }
        if (value.Z > light.Z)
        {
            light.Z = value.Z;
        }
    }

    protected void SpreadLightLine(
        Vector3[] lightMap,
        Vec3 color,
        int index,
        int distance,
        int indexChange
    )
    {
        // Performance optimization
        float[][] lightMask = _lightMask;
        float[] airDecay = _lightAirDecay;
        float[] solidDecay = _lightSolidDecay;
        float lightLoss = _lightLossExitingSolid;

        index += indexChange;
        SetLight(ref lightMap[index], color);

        // Would multiply by (distance + 1), but we already incremented index once
        int endIndex = index + distance * indexChange;
        float[] prevMask = lightMask[index];
        while (true)
        {
            index += indexChange;
            if (index == endIndex)
            {
                break;
            }

            float[] mask = lightMask[index];
            if (prevMask == solidDecay && mask == airDecay)
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
    protected void UpdateTemporalData(
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
        const float LOG_BASE_DECAY = -3.218876f; // log(0.04)

        int baseWork = Math.Clamp(
            (int)Math.Ceiling(
                (
                    LOG_BASE_DECAY
                    - MathF.Log(Math.Max(color.X, Math.Max(color.Y, color.Z)))
                ) * _reciprocalLogSlowestDecay
            ) + 1,
            1,
            MAX_LIGHT_RANGE
        );

        int approximateWorkDone
            = 1
            + (
                    (doUp ? 1 : 0)
                    + (doDown ? 1 : 0)
                    + (doLeft ? 1 : 0)
                    + (doRight ? 1 : 0)
                ) * baseWork
            + (
                    (doUpperLeft ? 1 : 0)
                    + (doUpperRight ? 1 : 0)
                    + (doLowerLeft ? 1 : 0)
                    + (doLowerRight ? 1 : 0)
                ) * (baseWork * baseWork);

        Interlocked.Add(ref _temporalData, approximateWorkDone);
    }
}
