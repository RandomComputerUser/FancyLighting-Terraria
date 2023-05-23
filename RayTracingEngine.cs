using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class RayTracingEngine : IFancyLightingEngine
{
    private const float LIGHT_MULT = 65535f / 16384;
    private const float RECIPROCAL_LIGHT_MULT = 16384f / 65535f;

    private Rectangle _lightMapArea;

    private Rgba64[] _lightSource;
    private Rgba64[] _lightDestination;
    private Texture2D _lightSourceTexture;
    private Texture2D _lightDestinationTexture;

    private Shader _rayTracingShader;

    private readonly Texture2D _noiseTexture;

    private RenderTarget2D _tmpTarget;
    private SpriteBatch _spriteBatch;

    public RayTracingEngine()
    {
        _rayTracingShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/RayTracing",
            "RayTracedLighting"
        );

        _noiseTexture = ModContent.Request<Texture2D>(
            "FancyLighting/Effects/Noise", ReLogic.Content.AssetRequestMode.ImmediateLoad
        ).Value;

        Main.QueueMainThreadAction(
            () => _spriteBatch = new(Main.instance.GraphicsDevice)
        );
    }

    public void Unload()
    {
        _lightSourceTexture?.Dispose();
        _lightDestinationTexture?.Dispose();

        EffectLoader.UnloadEffect(ref _rayTracingShader);

        _noiseTexture?.Dispose();

        _tmpTarget?.Dispose();
        _spriteBatch?.Dispose();
    }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightDecay,
        int width,
        int height
    )
    {
        if (_spriteBatch is null)
        {
            return;
        }

        const float MAX_DECAY_VALUE = 0.97f;

        float decayMult = LightingConfig.Instance.FancyLightingEngineMakeBrighter ? 1f : 0.975f;
        float lightAirDecay
            = decayMult * Math.Min(lightMap.LightDecayThroughAir, MAX_DECAY_VALUE);
        float lightSolidDecay = decayMult * Math.Min(
            MathF.Pow(
                lightMap.LightDecayThroughSolid,
                LightingConfig.Instance.FancyLightingEngineAbsorptionExponent()
            ),
            MAX_DECAY_VALUE
        );
        float lightWaterDecay = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughWater.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughWater.X,
                Math.Max(lightMap.LightDecayThroughWater.Y, lightMap.LightDecayThroughWater.Z)
            ),
            MAX_DECAY_VALUE
        );
        float lightHoneyDecay = decayMult * Math.Min(
            0.625f * lightMap.LightDecayThroughHoney.Length() / Vector3.One.Length()
            + 0.375f * Math.Max(
                lightMap.LightDecayThroughHoney.X,
                Math.Max(lightMap.LightDecayThroughHoney.Y, lightMap.LightDecayThroughHoney.Z)
            ),
            MAX_DECAY_VALUE
        );
        float lightShadowPaintDecay = 0f;

        int size = width * height;

        if (_lightSource is null || _lightSource.Length < size)
        {
            _lightSource = new Rgba64[size];
            _lightDestination = new Rgba64[size];
        }

        int topEdgeY = _lightMapArea.Y + height - 1;
        Parallel.For(
            0,
            width,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (i) =>
            {
                int x = i + _lightMapArea.X;
                int y = _lightMapArea.Y;
                bool notOnVerticalEdge = i > 0 && i < width - 1;
                int endIndex = height * (i + 1);
                for (int j = height * i; j < endIndex; ++j)
                {
                    float decay = lightDecay[j] switch
                    {
                        LightMaskMode.Solid
                            => Main.tile[x, y].TileColor == PaintID.ShadowPaint
                                ? lightShadowPaintDecay
                                : lightSolidDecay,
                        LightMaskMode.Water => lightWaterDecay,
                        LightMaskMode.Honey => lightHoneyDecay,
                        _ => lightAirDecay,
                    };

                    Vector3 color = colors[j];
                    if (
                        lightDecay[j] is not LightMaskMode.Solid
                        && notOnVerticalEdge && y > _lightMapArea.Y && y < topEdgeY
                    )
                    {
                        // Extremely basic denoising
                        color = Vector3.Max(
                            color,
                            0.5f * Vector3.Max(
                                Vector3.Max(colors[j - 1], colors[j + 1]),
                                Vector3.Max(colors[j - height], colors[j + height])
                            )
                        );
                    }

                    VectorToColor.Assign(ref _lightSource[j], RECIPROCAL_LIGHT_MULT, color, decay);

                    ++y;
                }
            }
        );

        RenderTarget2D mainRenderTarget = MainRenderTarget.Get();
        if (mainRenderTarget is not null)
        {
            TextureMaker.MakeAtLeastSizeColor(ref _tmpTarget, mainRenderTarget.Width, mainRenderTarget.Height);
            Main.instance.GraphicsDevice.SetRenderTarget(_tmpTarget);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            _spriteBatch.Begin();
            _spriteBatch.Draw(mainRenderTarget, Vector2.Zero, Color.White);
            _spriteBatch.End();
        }

        TextureMaker.MakeSizeRgba64(ref _lightSourceTexture, height, width);
        TextureMaker.MakeSizeRgba64(ref _lightDestinationTexture, height, width);

        _lightSourceTexture.SetData(_lightSource, 0, size);

        Main.instance.GraphicsDevice.SetRenderTarget((RenderTarget2D)_lightDestinationTexture);
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

        try
        {
            Main.instance.GraphicsDevice.Textures[1] = _noiseTexture;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.PointWrap;

            long time = Environment.TickCount64 / 500;
            float lerp = (float)(Environment.TickCount64 % 500 / 500.0);

            _rayTracingShader.SetParameter("LightmapSize", new Vector2(height, width))
                .SetParameter("ReciprocalLightmapSize", new Vector2(1f / height, 1f / width))
                .SetParameter("SolidThreshold", lightSolidDecay + 0.001f)
                .SetParameter("SolidExitLightLoss", LightingConfig.Instance.FancyLightingEngineExitMultiplier())
                .SetParameter("NoiseCoordMult", new Vector2(
                    (float)height / _noiseTexture.Width,
                    (float)width / _noiseTexture.Height))
                .SetParameter("NoiseLerp", lerp)
                .SetParameter("Offset1", new Vector2(
                    (float)((time + _lightMapArea.Y) % _noiseTexture.Width / (double)_noiseTexture.Width),
                    (float)((time / _noiseTexture.Width + _lightMapArea.X)
                        % _noiseTexture.Height / (double)_noiseTexture.Height)))
                .SetParameter("Offset2", new Vector2(
                    (float)(((time + 1) + _lightMapArea.Y) % _noiseTexture.Width / (double)_noiseTexture.Width),
                    (float)(((time + 1) / _noiseTexture.Width + _lightMapArea.X)
                        % _noiseTexture.Height / (double)_noiseTexture.Height)))
                .Apply();

            _spriteBatch.Draw(_lightSourceTexture, Vector2.Zero, Color.White);
        }
        finally
        {
            _spriteBatch.End();
        }

        Main.instance.GraphicsDevice.SetRenderTarget(mainRenderTarget);
        if (mainRenderTarget is not null)
        {
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            _spriteBatch.Begin();
            _spriteBatch.Draw(_tmpTarget, Vector2.Zero, Color.White);
            _spriteBatch.End();
        }

        _lightDestinationTexture.GetData(_lightDestination, 0, size);

        Parallel.For(
            0,
            width,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (i) =>
            {
                int endIndex = height * (i + 1);
                for (int j = height * i; j < endIndex; ++j)
                {
                    Vector4 light = _lightDestination[j].ToVector4();
                    ref Vector3 color = ref colors[j];

                    color.X = LIGHT_MULT * light.X;
                    color.Y = LIGHT_MULT * light.Y;
                    color.Z = LIGHT_MULT * light.Z;
                }
            }
        );
    }
}
