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

internal abstract class FancyLightingEngineBase<WorkingLightType> : ICustomLightingEngine
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;
    protected int _temporalData;

    private const float LOW_LIGHT_LEVEL = 0.03f;

    protected float _initialBrightnessCutoff;
    protected float _logBrightnessCutoff;
    protected float _reciprocalLogSlowestDecay;
    protected float _lightLossExitingSolid;

    protected float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    protected float[] _lightWaterDecay;
    protected float[] _lightHoneyDecay;
    protected float[] _lightShadowPaintDecay; // In vanilla shadow paint isn't a special case

    protected float[][] _lightMask;

    private Task[] _tasks;
    private Vector3[][] _workingLightMaps;
    private WorkingLightType[][] _workingLights;

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

    protected static void CalculateSubTileLightingSpread(
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

    protected void ComputeCircles(int maxLightRange)
    {
        _circles = new int[maxLightRange + 1][];
        _circles[0] = new int[] { 0 };
        for (int radius = 1; radius < maxLightRange + 1; ++radius)
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

    protected void UpdateDecays(
        LightMap lightMap,
        int exponentDivisor,
        float maxDecayMult = 0.95f
    )
    {
        float decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
        float lightAirDecayBaseline
            = decayMult * Math.Min(lightMap.LightDecayThroughAir, maxDecayMult);
        float lightSolidDecayBaseline = decayMult * Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            maxDecayMult
        );
        float lightWaterDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughWater.X,
                Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
            ),
            maxDecayMult
        );
        float lightHoneyDecayBaseline = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughHoney.X,
                Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
            ),
            maxDecayMult
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

        _reciprocalLogSlowestDecay = 1f / MathF.Log(
            Math.Max(
                Math.Max(lightAirDecayBaseline, lightSolidDecayBaseline),
                Math.Max(lightWaterDecayBaseline, lightHoneyDecayBaseline)
            )
        );

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline, exponentDivisor);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline, exponentDivisor);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline, exponentDivisor);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline, exponentDivisor);
    }

    private static void UpdateDecay(float[] decay, float baseline, int distanceTicks)
    {
        if (baseline == decay[distanceTicks])
        {
            return;
        }

        float logBaseline = MathF.Log(baseline);
        float exponentMult = 1f / distanceTicks;
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

    protected void ConvertLightColorsToLinear(Vector3[] colors, int width, int height)
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

    protected void InitializeTaskVariables(int lightMapSize, int maxLightRange)
    {
        int taskCount = LightingConfig.Instance.ThreadCount;

        if (_tasks is null)
        {
            _tasks = new Task[taskCount];
            _workingLightMaps = new Vector3[taskCount][];
            _workingLights = new WorkingLightType[taskCount][];

            for (int i = 0; i < taskCount; ++i)
            {
                _workingLightMaps[i] = new Vector3[lightMapSize];
                _workingLights[i] = new WorkingLightType[maxLightRange + 1];
            }
        }
        else if (_tasks.Length != taskCount)
        {
            _tasks = new Task[taskCount];

            Vector3[][] workingLightMaps = new Vector3[taskCount][];
            WorkingLightType[][] workingLights = new WorkingLightType[taskCount][];
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

                workingLights[i] = _workingLights[i];
            }

            for (int i = numToCopy; i < taskCount; ++i)
            {
                workingLightMaps[i] = new Vector3[lightMapSize];
                workingLights[i] = new WorkingLightType[maxLightRange + 1];
            }

            _workingLightMaps = workingLightMaps;
            _workingLights = workingLights;
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
        Action<Vector3[], WorkingLightType[], int> lightingAction
    )
    {
        int taskCount = LightingConfig.Instance.ThreadCount;

        if (taskCount <= 1)
        {
            Vector3[] workingLightMap = _workingLightMaps[0];
            WorkingLightType[] workingLights = _workingLights[0];

            Array.Copy(initialLightMapValue, workingLightMap, lightMapSize);

            for (int i = 0; i < lightMapSize; ++i)
            {
                lightingAction(workingLightMap, workingLights, i);
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
                    WorkingLightType[] workingLights = _workingLights[index];

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
                            lightingAction(workingLightMap, workingLights, i);
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

                // Potentially faster than Math.Max, which checks for NaN and signed zero

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

                // Potentially faster than Math.Max, which checks for NaN and signed zero
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

    protected void GetLightsForGlobalIllumination(
        Vector3[] source,
        Vector3[] destination,
        Vector3[] lightSources,
        bool[] skipGI,
        LightMaskMode[] lightMasks,
        int width,
        int height,
        float giMult
    )
    {
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
                int x = i + _lightMapArea.X;
                int y = _lightMapArea.Y;
                int endIndex = height * (i + 1);
                for (int j = height * i; j < endIndex; ++j, ++y)
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
}
