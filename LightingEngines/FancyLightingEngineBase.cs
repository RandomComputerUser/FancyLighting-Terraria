using FancyLighting.Config;
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

    protected float[] _lightAirDecay;
    protected float[] _lightSolidDecay;
    protected float[] _lightWaterDecay;
    protected float[] _lightHoneyDecay;
    protected float[] _lightShadowPaintDecay; // In vanilla shadow paint isn't a special case

    protected float[][] _lightMask;

    private Task[] _tasks;
    private Vector3[][] _workingLightMap;
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

    protected static void UpdateDecay(float[] decay, float baseline, int exponentDivisor)
    {
        if (baseline == decay[exponentDivisor])
        {
            return;
        }

        float logBaseline = MathF.Log(baseline);
        float exponentMult = 1f / exponentDivisor;
        for (int i = 0; i < decay.Length; ++i)
        {
            decay[i] = MathF.Exp(exponentMult * i * logBaseline);
        }
    }

    protected void UpdateDecays(
        LightMap lightMap,
        float maxDecayMult,
        int exponentDivisor
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

        UpdateDecay(_lightAirDecay, lightAirDecayBaseline, exponentDivisor);
        UpdateDecay(_lightSolidDecay, lightSolidDecayBaseline, exponentDivisor);
        UpdateDecay(_lightWaterDecay, lightWaterDecayBaseline, exponentDivisor);
        UpdateDecay(_lightHoneyDecay, lightHoneyDecayBaseline, exponentDivisor);
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

    protected void InitializeTaskVariables(int lightMapSize, int maxLightRange)
    {
        int taskCount = LightingConfig.Instance.ThreadCount;

        if (_tasks is null)
        {
            _tasks = new Task[taskCount];
            _workingLightMap = new Vector3[taskCount][];
            _workingLights = new WorkingLightType[taskCount][];

            for (int i = 0; i < taskCount; ++i)
            {
                _workingLightMap[i] = new Vector3[lightMapSize];
                _workingLights[i] = new WorkingLightType[maxLightRange + 1];
            }
        }
        else if (_tasks.Length != taskCount)
        {
            _tasks = new Task[taskCount];

            Vector3[][] workingLightMap = new Vector3[taskCount][];
            WorkingLightType[][] workingLights = new WorkingLightType[taskCount][];
            int numToCopy = Math.Min(_workingLightMap.Length, taskCount);

            for (int i = 0; i < numToCopy; ++i)
            {
                if (_workingLightMap[i].Length >= lightMapSize)
                {
                    workingLightMap[i] = _workingLightMap[i];
                }
                else
                {
                    workingLightMap[i] = new Vector3[lightMapSize];
                }

                workingLights[i] = _workingLights[i];
            }

            for (int i = numToCopy; i < taskCount; ++i)
            {
                workingLightMap[i] = new Vector3[lightMapSize];
                workingLights[i] = new WorkingLightType[maxLightRange + 1];
            }

            _workingLightMap = workingLightMap;
            _workingLights = workingLights;
        }
        else
        {
            for (int i = 0; i < taskCount; ++i)
            {
                if (_workingLightMap[i].Length < lightMapSize)
                {
                    _workingLightMap[i] = new Vector3[lightMapSize];
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
            Vector3[] workingLightMap = _workingLightMap[0];
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

                    Vector3[] workingLightMap = _workingLightMap[index];
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

        static void MaxArraysIntoFirst(Vector3[] arr1, Vector3[] arr2, int length)
        {
            for (int i = 0; i < length; ++i)
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

        static void MaxArrays(Vector3[] arr1, Vector3[] arr2, Vector3[] result, int length)
        {
            for (int i = 0; i < length; ++i)
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

        for (int increment = 1; ; increment *= 2)
        {
            int iterationCount = taskCount / (2 * increment);
            if (taskCount % (2 * increment) > increment)
            {
                ++iterationCount;
            }

            if (iterationCount == 1)
            {
                MaxArrays(_workingLightMap[0], _workingLightMap[increment], destination, lightMapSize);
                return;
            }

            Parallel.For(
                0,
                iterationCount,
                new ParallelOptions { MaxDegreeOfParallelism = taskCount },
                (i) =>
                    MaxArraysIntoFirst(
                        _workingLightMap[increment * (2 * i)],
                        _workingLightMap[increment * (2 * i + 1)],
                        lightMapSize
                    )
            );
        }
    }
}
