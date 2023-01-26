using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using System;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class SmoothLighting
{
    private Texture2D _colors;
    private Texture2D _colorsBackground;

    private readonly Texture2D _ditherMask;

    private Vector2 _lightMapPosition;
    private Vector2 _lightMapPositionFlipped;
    private Rectangle _lightMapTileArea;
    private Rectangle _lightMapRenderArea;

    private RenderTarget2D _drawTarget1;
    private RenderTarget2D _drawTarget2;

    private Vector3[] _lights;
    private Color[] _finalLights;
    private Rgba64[] _finalLightsHiDef;

    internal Vector3[] _whiteLights;
    internal Vector3[] _tmpLights;

    private readonly bool[] _glowingTiles;
    private readonly Color[] _glowingTileColors;

    private bool _isDangersenseActive;
    private bool _isSpelunkerActive;

    private bool _smoothLightingLightMapValid;
    private bool _smoothLightingPositionValid;
    private bool _smoothLightingBackComplete;
    private bool _smoothLightingForeComplete;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;
    private RenderTarget2D _cameraModeTarget3;

    internal bool DrawSmoothLightingBack
        => _smoothLightingBackComplete && LightingConfig.Instance.SmoothLightingEnabled();

    internal bool DrawSmoothLightingFore
        => _smoothLightingForeComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private readonly FancyLightingMod _modInstance;

    private int _printExceptionTime;

    private Shader _bicubicShader;
    private Shader _bicubicNoDitherHiDefShader;
    private Shader _noFilterShader;
    private Shader _qualityNormalsShader;
    private Shader _qualityNormalsOverbrightShader;
    private Shader _qualityNormalsOverbrightLightOnlyHiDefShader;
    private Shader _normalsShader;
    private Shader _normalsOverbrightShader;
    private Shader _normalsOverbrightLightOnlyHiDefShader;
    private Shader _overbrightShader;
    private Shader _overbrightLightOnlyHiDefShader;
    private Shader _overbrightMaxShader;

    public SmoothLighting(FancyLightingMod mod)
    {
        _modInstance = mod;

        _lightMapTileArea = new Rectangle(0, 0, 0, 0);
        _lightMapRenderArea = new Rectangle(0, 0, 0, 0);

        _smoothLightingLightMapValid = false;
        _smoothLightingPositionValid = false;
        _smoothLightingForeComplete = false;
        _smoothLightingBackComplete = false;

        _glowingTiles = new bool[ushort.MaxValue + 1];
        foreach (ushort id in new ushort[] {
            TileID.Crystals,
            TileID.LavaMoss,
            TileID.LavaMossBrick,
            TileID.ArgonMoss,
            TileID.ArgonMossBrick,
            TileID.KryptonMoss,
            TileID.KryptonMossBrick,
            TileID.XenonMoss,
            TileID.XenonMossBrick,
            TileID.MeteoriteBrick,
            TileID.MartianConduitPlating,
            TileID.LavaLamp
        })
        {
            _glowingTiles[id] = true;
        }

        _glowingTileColors = new Color[_glowingTiles.Length];

        _glowingTileColors[TileID.Crystals] = Color.White;

        _glowingTileColors[TileID.LavaMoss] = _glowingTileColors[TileID.LavaMossBrick] = new Color(254, 122, 0);
        _glowingTileColors[TileID.ArgonMoss] = _glowingTileColors[TileID.ArgonMossBrick] = new Color(254, 92, 186);
        _glowingTileColors[TileID.KryptonMoss] = _glowingTileColors[TileID.KryptonMossBrick] = new Color(215, 255, 0);
        _glowingTileColors[TileID.XenonMoss] = _glowingTileColors[TileID.XenonMossBrick] = new Color(0, 254, 242);

        _glowingTileColors[TileID.MeteoriteBrick] = new Color(219, 104, 19);
        // Martian Conduit Plating is handled separately
        _glowingTileColors[TileID.LavaLamp] = new Color(255, 90, 2);

        _isDangersenseActive = false;
        _isSpelunkerActive = false;

        _bicubicShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "Bicubic"
        );
        _bicubicNoDitherHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "BicubicNoDitherHiDef"
        );
        _noFilterShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/Upscaling",
            "NoFilter"
        );

        _qualityNormalsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "QualityNormals",
            true
        );
        _qualityNormalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "QualityNormalsOverbright",
            true
        );
        _qualityNormalsOverbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "QualityNormalsOverbrightLightOnlyHiDef"
        );
        _normalsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "Normals"
        );
        _normalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "NormalsOverbright",
            true
        );
        _normalsOverbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "NormalsOverbrightLightOnlyHiDef"
        );
        _overbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "Overbright",
            true
        );
        _overbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "OverbrightLightOnlyHiDef"
        );
        _overbrightMaxShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/NormalMap",
            "OverbrightMax",
            true
        );

        _ditherMask = ModContent.Request<Texture2D>(
            "FancyLighting/Effects/DitheringMask", ReLogic.Content.AssetRequestMode.ImmediateLoad
        ).Value;
    }

    public void Unload()
    {
        _drawTarget1?.Dispose();
        _drawTarget2?.Dispose();
        _colors?.Dispose();
        _colorsBackground?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _cameraModeTarget3?.Dispose();
        _ditherMask?.Dispose();
        EffectLoader.UnloadEffect(ref _bicubicShader);
        EffectLoader.UnloadEffect(ref _bicubicNoDitherHiDefShader);
        EffectLoader.UnloadEffect(ref _noFilterShader);
        EffectLoader.UnloadEffect(ref _qualityNormalsShader);
        EffectLoader.UnloadEffect(ref _qualityNormalsOverbrightShader);
        EffectLoader.UnloadEffect(ref _normalsShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightShader);
        EffectLoader.UnloadEffect(ref _overbrightShader);
        EffectLoader.UnloadEffect(ref _overbrightMaxShader);
    }

    private void PrintException()
    {
        LightingConfig.Instance.UseSmoothLighting = false;
        Main.NewText(
            "[Fancy Lighting] Caught an IndexOutOfRangeException while trying to run smooth lighting",
            Color.Orange
        );
        Main.NewText(
            "[Fancy Lighting] Smooth lighting has been automatically disabled",
            Color.Orange
        );
    }

    internal void GetAndBlurLightMap(Vector3[] colors, int width, int height)
    {
        _smoothLightingLightMapValid = false;
        _smoothLightingPositionValid = false;
        _smoothLightingBackComplete = false;
        _smoothLightingForeComplete = false;

        if (_lights is null || _lights.Length < height * width)
        {
            _lights = new Vector3[height * width];
        }
        if (_whiteLights is null || _whiteLights.Length < height * width)
        {
            _whiteLights = new Vector3[height * width];
        }

        if (width == 0 || height == 0)
        {
            return;
        }

        if (colors.Length < height * width)
        {
            return;
        }

        int caughtException = 0;

        if (LightingConfig.Instance.UseLightMapBlurring)
        {
            Parallel.For(
                1,
                width - 1,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x) =>
                {
                    int i = height * x;
                    for (int y = 1; y < height - 1; ++y)
                    {
                        ++i;

                        try
                        {
                            // Faster to do it separately for each component
                            _lights[i].X = (
                                  1f * colors[i - height - 1].X + 2f * colors[i - 1].X + 1f * colors[i + height - 1].X
                                + 2f * colors[i - height].X + 4f * colors[i].X + 2f * colors[i + height].X
                                + 1f * colors[i - height + 1].X + 2f * colors[i + 1].X + 1f * colors[i + height + 1].X
                            ) / 16f;

                            _lights[i].Y = (
                                  1f * colors[i - height - 1].Y + 2f * colors[i - 1].Y + 1f * colors[i + height - 1].Y
                                + 2f * colors[i - height].Y + 4f * colors[i].Y + 2f * colors[i + height].Y
                                + 1f * colors[i - height + 1].Y + 2f * colors[i + 1].Y + 1f * colors[i + height + 1].Y
                            ) / 16f;

                            _lights[i].Z = (
                                  1f * colors[i - height - 1].Z + 2f * colors[i - 1].Z + 1f * colors[i + height - 1].Z
                                + 2f * colors[i - height].Z + 4f * colors[i].Z + 2f * colors[i + height].Z
                                + 1f * colors[i - height + 1].Z + 2f * colors[i + 1].Z + 1f * colors[i + height + 1].Z
                            ) / 16f;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            int offset = (width - 1) * height;
            for (int i = 0; i < height; ++i)
            {
                try
                {
                    _lights[i] = colors[i];
                    _lights[i + offset] = colors[i + offset];
                }
                catch (IndexOutOfRangeException)
                {
                    PrintException();
                    return;
                }
            }

            int end = (width - 1) * height;
            offset = height - 1;
            for (int i = height; i < end; i += height)
            {
                try
                {
                    _lights[i] = colors[i];
                    _lights[i + offset] = colors[i + offset];
                }
                catch (IndexOutOfRangeException)
                {
                    PrintException();
                    return;
                }
            }

            Array.Copy(_lights, colors, height * width);
        }
        else
        {
            Array.Copy(colors, _lights, height * width);
        }

        if (LightingConfig.Instance.UseNormalMaps())
        {
            Parallel.For(
                1,
                width - 1,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x) =>
                {
                    int i = height * x + 1;
                    for (int y = 1; y < height - 1; ++y)
                    {
                        try
                        {
                            ref Vector3 color = ref _lights[i];
                            if (color.X >= 1f / 255f || color.Y >= 1f / 255f || color.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            Vector3 otherColor;

                            otherColor = _lights[i - 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i + 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i - height];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i + height];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i - height - 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i - height + 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i + height - 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            otherColor = _lights[i + height + 1];
                            if (otherColor.X >= 1f / 255f || otherColor.Y >= 1f / 255f || otherColor.Z >= 1f / 255f)
                            {
                                _whiteLights[i++] = Vector3.One;
                                continue;
                            }

                            _whiteLights[i++] = new Vector3(1f / 255f, 1f / 255f, 1f / 255f);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }
                    }
                }
            );
        }
        else
        {
            Parallel.For(
                0,
                width,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x) =>
                {
                    int i = height * x;
                    for (int y = 0; y < height; ++y)
                    {
                        try
                        {
                            ref Vector3 color = ref _lights[i];
                            _whiteLights[i++] = color.X < 1f / 255f && color.Y < 1f / 255f && color.Z < 1f / 255f
                                ? new Vector3(1f / 255f, 1f / 255f, 1f / 255f)
                                : Vector3.One;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }
                    }
                }
            );
        }

        LightingEngine lightEngine = (LightingEngine)_modInstance.field_activeEngine.GetValue(null);
        _lightMapTileArea = (Rectangle)_modInstance.field_workingProcessedArea.GetValue(lightEngine);
        _lightMapRenderArea = new Rectangle(0, 0, _lightMapTileArea.Height, _lightMapTileArea.Width);

        _smoothLightingLightMapValid = true;
    }

    private void GetColorsPosition(bool cameraMode)
    {
        int xmin = _lightMapTileArea.X;
        int ymin = _lightMapTileArea.Y;
        int width = _lightMapTileArea.Width;
        int height = _lightMapTileArea.Height;

        if (width == 0 || height == 0)
        {
            return;
        }

        _lightMapPosition = 16f * new Vector2(xmin + width, ymin);
        _lightMapPositionFlipped = 16f * new Vector2(xmin, ymin + height);

        _smoothLightingPositionValid = !cameraMode;
    }

    internal void CalculateSmoothLighting(bool background, bool cameraMode = false)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        _isDangersenseActive = Main.LocalPlayer.dangerSense;
        _isSpelunkerActive = Main.LocalPlayer.findTreasure;

        if (!_smoothLightingPositionValid || cameraMode)
        {
            GetColorsPosition(cameraMode);
        }

        if (!_smoothLightingPositionValid && !cameraMode)
        {
            return;
        }

        if (Main.tile.Height == 0 || Main.tile.Width == 0)
        {
            return;
        }

        int xmin = _lightMapTileArea.X;
        int ymin = _lightMapTileArea.Y;
        int width = _lightMapTileArea.Width;
        int height = _lightMapTileArea.Height;
        int ymax = ymin + height;

        int clampedXmin = Math.Clamp(xmin, 0, Main.tile.Width);
        int clampedXmax = Math.Clamp(xmin + width, 0, Main.tile.Width);
        if (clampedXmax - clampedXmin < 1)
        {
            return;
        }

        int clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
        int clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
        if (clampedEnd - clampedStart < 1)
        {
            return;
        }

        int clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
        int clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
        if (clampedYmax - clampedYmin < 1)
        {
            return;
        }

        int offset = clampedYmin - ymin;
        if (offset < 0 || offset >= height)
        {
            return;
        }

        if (LightingConfig.Instance.HiDefFeaturesEnabled())
        {
            CalculateSmoothLightingHiDef(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                background,
                cameraMode
            );
        }
        else
        {
            CalculateSmoothLightingReach(
                xmin,
                clampedYmin,
                clampedYmax,
                clampedStart,
                clampedEnd,
                offset,
                width,
                height,
                background,
                cameraMode
            );
        }
    }

    private void CalculateSmoothLightingHiDef(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool background,
        bool cameraMode
    )
    {
        if (_finalLightsHiDef is null || _finalLightsHiDef.Length < height * width)
        {
            _finalLightsHiDef = new Rgba64[height * width];
        }

        int caughtException = 0;

        const int overbrightWhite = 16384;
        const float overbrightMult = overbrightWhite / 65535f;

        Rgba64 whiteLight = new(Vector4.One);
        float brightness = Lighting.GlobalBrightness;
        float fullBrightness = brightness;
        float multFromOverbright;
        if (LightingConfig.Instance.DrawOverbright())
        {
            ColorConverter.Assign(ref whiteLight, 1f, new Vector3(overbrightMult));
            fullBrightness *= overbrightMult;
            multFromOverbright = overbrightMult;
        }
        else
        {
            multFromOverbright = 1f;
        }

        if (background && (!_smoothLightingBackComplete || cameraMode))
        {
            Parallel.For(
                clampedStart,
                clampedEnd,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x1) =>
                {
                    int i = height * x1 + offset;
                    int x = x1 + xmin;
                    for (int y = clampedYmin; y < clampedYmax; ++y)
                    {
                        try
                        {
                            // Illuminant Paint
                            if (Main.tile[x, y].WallColor == PaintID.IlluminantPaint)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            ColorConverter.Assign(ref _finalLightsHiDef[i], fullBrightness, _lights[i]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }

                        ++i;
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureMaker.MakeAtLeastSize(ref _colorsBackground, height, width);

            _colorsBackground.SetData(0, _lightMapRenderArea, _finalLightsHiDef, 0, height * width);

            _smoothLightingBackComplete = !cameraMode;
        }
        else if (!background && (!_smoothLightingForeComplete || cameraMode))
        {
            _glowingTileColors[TileID.MartianConduitPlating] = new Color(new Vector3(
                (float)(0.4 - 0.4 * Math.Cos(
                    (int)(0.08 * Main.timeForVisualEffects / 6.283) % 3 == 1
                        ? 0.08 * Main.timeForVisualEffects
                        : 0.0
                    )
                )
            ));

            Parallel.For(
                clampedStart,
                clampedEnd,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x1) =>
                {
                    int i = height * x1 + offset;
                    int x = x1 + xmin;
                    for (int y = clampedYmin; y < clampedYmax; ++y)
                    {
                        try
                        {
                            // Illuminant Paint
                            if (Main.tile[x, y].TileColor == PaintID.IlluminantPaint)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            Vector3.Multiply(ref _lights[i], brightness, out Vector3 lightColor);

                            // Crystal Shards, Gelatin Crystal, Glowing Moss, Meteorite Brick, and Martian Conduit Plating
                            if (_glowingTiles[Main.tile[x, y].TileType])
                            {
                                ref Color glow = ref _glowingTileColors[Main.tile[x, y].TileType];

                                lightColor.X = Math.Max(lightColor.X, glow.R / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, glow.G / 255f);
                                lightColor.Z = Math.Max(lightColor.Z, glow.B / 255f);
                            }

                            // Dangersense Potion
                            else if (
                                _isDangersenseActive
                                && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                            )
                            {
                                lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                            }

                            // Spelunker Potion
                            else if (_isSpelunkerActive && Main.IsTileSpelunkable(x, y))
                            {
                                lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                            }

                            ColorConverter.Assign(ref _finalLightsHiDef[i], multFromOverbright, lightColor);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }

                        ++i;
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureMaker.MakeAtLeastSize(ref _colors, height, width);

            _colors.SetData(0, _lightMapRenderArea, _finalLightsHiDef, 0, height * width);

            _smoothLightingForeComplete = !cameraMode;
        }
    }

    private void CalculateSmoothLightingReach(
        int xmin,
        int clampedYmin,
        int clampedYmax,
        int clampedStart,
        int clampedEnd,
        int offset,
        int width,
        int height,
        bool background,
        bool cameraMode
    )
    {
        if (_finalLights is null || _finalLights.Length < height * width)
        {
            _finalLights = new Color[height * width];
        }

        int caughtException = 0;

        const int overbrightWhite = 128;
        const float overbrightMult = overbrightWhite / 255f;

        Color whiteLight;
        float brightness = Lighting.GlobalBrightness;
        float fullBrightness = brightness;
        float multFromOverbright;
        if (LightingConfig.Instance.DrawOverbright())
        {
            whiteLight = new Color(overbrightWhite, overbrightWhite, overbrightWhite);
            fullBrightness *= overbrightMult;
            multFromOverbright = overbrightMult;
        }
        else
        {
            whiteLight = Color.White;
            multFromOverbright = 1f;
        }

        if (background && (!_smoothLightingBackComplete || cameraMode))
        {
            Parallel.For(
                clampedStart,
                clampedEnd,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x1) =>
                {
                    int i = height * x1 + offset;
                    int x = x1 + xmin;
                    for (int y = clampedYmin; y < clampedYmax; ++y)
                    {
                        try
                        {
                            // Illuminant Paint
                            if (Main.tile[x, y].WallColor == PaintID.IlluminantPaint)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            ColorConverter.Assign(ref _finalLights[i], fullBrightness, _lights[i]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }

                        ++i;
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureMaker.MakeAtLeastSize(ref _colorsBackground, height, width);

            _colorsBackground.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

            _smoothLightingBackComplete = !cameraMode;
        }
        else if (!background && (!_smoothLightingForeComplete || cameraMode))
        {
            _glowingTileColors[TileID.MartianConduitPlating] = new Color(new Vector3(
                (float)(0.4 - 0.4 * Math.Cos(
                    (int)(0.08 * Main.timeForVisualEffects / 6.283) % 3 == 1
                        ? 0.08 * Main.timeForVisualEffects
                        : 0.0
                    )
                )
            ));

            Parallel.For(
                clampedStart,
                clampedEnd,
                new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
                (x1) =>
                {
                    int i = height * x1 + offset;
                    int x = x1 + xmin;
                    for (int y = clampedYmin; y < clampedYmax; ++y)
                    {
                        try
                        {
                            // Illuminant Paint
                            if (Main.tile[x, y].TileColor == PaintID.IlluminantPaint)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            Vector3.Multiply(ref _lights[i], brightness, out Vector3 lightColor);

                            // Crystal Shards, Gelatin Crystal, Glowing Moss, Meteorite Brick, and Martian Conduit Plating
                            if (_glowingTiles[Main.tile[x, y].TileType])
                            {
                                ref Color glow = ref _glowingTileColors[Main.tile[x, y].TileType];

                                lightColor.X = Math.Max(lightColor.X, glow.R / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, glow.G / 255f);
                                lightColor.Z = Math.Max(lightColor.Z, glow.B / 255f);
                            }

                            // Dangersense Potion
                            else if (
                                _isDangersenseActive
                                && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                            )
                            {
                                lightColor.X = Math.Max(lightColor.X, 255f / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, 50f / 255f);
                                lightColor.Z = Math.Max(lightColor.Z, 50f / 255f);
                            }

                            // Spelunker Potion
                            else if (_isSpelunkerActive && Main.IsTileSpelunkable(x, y))
                            {
                                lightColor.X = Math.Max(lightColor.X, 200f / 255f);
                                lightColor.Y = Math.Max(lightColor.Y, 170f / 255f);
                            }

                            ColorConverter.Assign(ref _finalLights[i], multFromOverbright, lightColor);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                        }

                        ++i;
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }

            TextureMaker.MakeAtLeastSize(ref _colors, height, width);

            _colors.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

            _smoothLightingForeComplete = !cameraMode;
        }
    }

    internal void DrawSmoothLighting(
        RenderTarget2D target,
        bool background,
        bool disableNormalMaps = false,
        RenderTarget2D tempTarget = null
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!background && !_smoothLightingForeComplete)
        {
            return;
        }

        if (background && !_smoothLightingBackComplete)
        {
            return;
        }

        bool doScaling = tempTarget is not null;
        Vector2 offset;
        if (tempTarget is null)
        {
            TextureMaker.MakeSize(ref _drawTarget1, target.Width, target.Height);
            tempTarget = _drawTarget1;
            offset = new Vector2(Main.offScreenRange);
        }
        else
        {
            offset = (tempTarget.Size() - new Vector2(Main.screenWidth, Main.screenHeight)) / 2f;
        }

        Texture2D lightMapTexture = background ? _colorsBackground : _colors;

        if (LightingConfig.Instance.UseNormalMaps() || LightingConfig.Instance.DrawOverbright())
        {
            TextureMaker.MakeAtLeastSize(ref _drawTarget2, tempTarget.Width, tempTarget.Height);
        }

        ApplySmoothLighting(
            lightMapTexture,
            tempTarget,
            _drawTarget2,
            _lightMapPosition,
            Main.screenPosition - offset,
            doScaling && !FancyLightingMod._inCameraMode ? Main.GameViewMatrix.Zoom : Vector2.One,
            target,
            background,
            disableNormalMaps,
            doScaling
        );

        Main.instance.GraphicsDevice.SetRenderTarget(target);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(
            tempTarget,
            Vector2.Zero,
            Color.White
        );
        Main.spriteBatch.End();
        Main.instance.GraphicsDevice.SetRenderTarget(null);
    }

    internal RenderTarget2D GetCameraModeRenderTarget(RenderTarget2D screenTarget)
    {
        TextureMaker.MakeSize(ref _cameraModeTarget1, screenTarget.Width, screenTarget.Height);
        return _cameraModeTarget1;
    }

    internal void DrawSmoothLightingCameraMode(
        RenderTarget2D screenTarget,
        RenderTarget2D target,
        bool background,
        bool skipFinalPass = false,
        bool disableNormalMaps = false
    )
    {
        Texture2D lightMapTexture = background ? _colorsBackground : _colors;

        TextureMaker.MakeAtLeastSize(ref _cameraModeTarget2, 16 * lightMapTexture.Height, 16 * lightMapTexture.Width);
        TextureMaker.MakeAtLeastSize(ref _cameraModeTarget3, 16 * lightMapTexture.Height, 16 * lightMapTexture.Width);

        if (LightingConfig.Instance.SmoothLightingEnabled())
        {
            ApplySmoothLighting(
                lightMapTexture,
                _cameraModeTarget2,
                _cameraModeTarget3,
                _lightMapPosition,
                16f * new Vector2(FancyLightingMod._cameraModeArea.X, FancyLightingMod._cameraModeArea.Y),
                Vector2.One,
                target,
                background,
                disableNormalMaps,
                false
            );
        }
        else
        {
            Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeTarget2);
            Main.instance.GraphicsDevice.Clear(Color.White);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend
            );

            Main.spriteBatch.Draw(
                target,
                Vector2.Zero,
                Color.White
            );

            Main.spriteBatch.End();
        }

        if (skipFinalPass)
        {
            return;
        }

        Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(
            screenTarget,
            Vector2.Zero,
            Color.White
        );
        Main.spriteBatch.End();

        Main.instance.GraphicsDevice.SetRenderTarget(screenTarget);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(
            _cameraModeTarget1,
            Vector2.Zero,
            Color.White
        );
        Main.spriteBatch.Draw(
            _cameraModeTarget2,
            Vector2.Zero,
            Color.White
        );
        Main.spriteBatch.End();
    }

    private void ApplySmoothLighting(
        Texture2D lightMapTexture,
        RenderTarget2D target1,
        RenderTarget2D target2,
        Vector2 lightMapPosition,
        Vector2 positionOffset,
        Vector2 zoom,
        RenderTarget2D worldTarget,
        bool background,
        bool disableNormalMaps,
        bool doScaling
    )
    {
        if (LightingConfig.Instance.RenderOnlyLight && background)
        {
            return;
        }

        bool qualityNormalMaps = LightingConfig.Instance.QualityNormalMaps;
        bool fineNormalMaps = LightingConfig.Instance.FineNormalMaps;
        bool doBicubicUpscaling = LightingConfig.Instance.UseBicubicScaling();
        bool simulateNormalMaps =
            !disableNormalMaps
            && LightingConfig.Instance.UseNormalMaps()
            && (!background || qualityNormalMaps);
        bool hiDef = LightingConfig.Instance.HiDefFeaturesEnabled();
        bool lightOnly = LightingConfig.Instance.RenderOnlyLight;
        bool doOverbright = LightingConfig.Instance.DrawOverbright() && !(lightOnly && !hiDef);
        bool noDithering = (qualityNormalMaps || doOverbright) && hiDef;

        Main.instance.GraphicsDevice.SetRenderTarget(simulateNormalMaps || doOverbright ? target2 : target1);
        Main.instance.GraphicsDevice.Clear(Color.White);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            FancyLightingMod.MultiplyBlend
        );

        if (doBicubicUpscaling)
        {
            if (noDithering)
            {
                _bicubicNoDitherHiDefShader
                    .UseShaderSpecificData(new Vector4(
                        1f / lightMapTexture.Width,
                        1f / lightMapTexture.Height,
                        lightMapTexture.Width,
                        lightMapTexture.Height))
                    .Apply();
            }
            else
            {
                _bicubicShader
                    .UseShaderSpecificData(new Vector4(
                        1f / lightMapTexture.Width,
                        1f / lightMapTexture.Height,
                        lightMapTexture.Width,
                        lightMapTexture.Height))
                    .UseColor(
                        16f * lightMapTexture.Width / _ditherMask.Width,
                        16f * lightMapTexture.Height / _ditherMask.Height,
                        0f)
                    .Apply();
                Main.instance.GraphicsDevice.Textures[1] = _ditherMask;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.PointWrap;
            }
        }

        bool flippedGravity = doScaling && Main.LocalPlayer.gravDir == -1 && !FancyLightingMod._inCameraMode;

        lightMapPosition -= positionOffset;
        float angle = (float)(Math.PI / 2.0);
        if (flippedGravity)
        {
            angle *= -1f;
            float top = 16f * _lightMapTileArea.Y - positionOffset.Y;
            float bottom = top + 16f * _lightMapTileArea.Height;
            float targetMiddle = worldTarget.Height / 2f;
            lightMapPosition.Y -= bottom - targetMiddle - (targetMiddle - top);
            lightMapPosition += _lightMapPositionFlipped - _lightMapPosition;
        }

        Main.spriteBatch.Draw(
            lightMapTexture,
            zoom * (lightMapPosition - target1.Size() / 2f) + target1.Size() / 2f,
            _lightMapRenderArea,
            Color.White,
            angle,
            Vector2.Zero,
            16f * new Vector2(zoom.Y, zoom.X),
            flippedGravity ? SpriteEffects.None : SpriteEffects.FlipVertically,
            0f
        );

        if (simulateNormalMaps || doOverbright)
        {
            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(target1);
            Main.instance.GraphicsDevice.Clear(Color.White);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend
            );

            MiscShaderData shader
            = simulateNormalMaps
                ? qualityNormalMaps
                    ? doOverbright
                        ? lightOnly
                            ? _qualityNormalsOverbrightLightOnlyHiDefShader
                            : _qualityNormalsOverbrightShader
                        : _qualityNormalsShader
                    : doOverbright
                        ? lightOnly
                            ? _normalsOverbrightLightOnlyHiDefShader
                            : _normalsOverbrightShader
                        : _normalsShader
                : doScaling // doOverbright is guaranteed to be true here
                    ? _overbrightMaxShader // if doScaling is true we're rendering tile entities
                    : lightOnly
                        ? _overbrightLightOnlyHiDefShader
                        : _overbrightShader;

            float normalMapRadius = qualityNormalMaps ? 30f : 25f;
            normalMapRadius *= LightingConfig.Instance.NormalMapsMultiplier();

            if (fineNormalMaps)
            {
                normalMapRadius *= 1.25f;
            }
            if (background)
            {
                normalMapRadius *= 0.75f;
            }

            float normalMapResolution = fineNormalMaps ? 1f : 2f;
            float hiDefNormalMapStrength = background ? 1f : 0.9f;

            shader
                .UseShaderSpecificData(new Vector4(
                    normalMapResolution / worldTarget.Width,
                    normalMapResolution / worldTarget.Height,
                    normalMapRadius / target2.Width,
                    normalMapRadius / target2.Height))
                .UseColor(
                    (float)target2.Width / worldTarget.Width,
                    (float)target2.Height / worldTarget.Height,
                    hiDefNormalMapStrength);
            Main.instance.GraphicsDevice.Textures[1] = worldTarget;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;
            if (noDithering)
            {
                shader.UseSecondaryColor(
                    (float)target2.Width / _ditherMask.Width,
                    (float)target2.Height / _ditherMask.Height,
                    0f);
                Main.instance.GraphicsDevice.Textures[2] = _ditherMask;
                Main.instance.GraphicsDevice.SamplerStates[2] = SamplerState.PointWrap;
            }
            shader.Apply();

            Main.spriteBatch.Draw(
                target2,
                Vector2.Zero,
                Color.White
            );
        }

        if (doBicubicUpscaling || simulateNormalMaps)
        {
            _noFilterShader.Apply();
        }

        if (!doOverbright && !LightingConfig.Instance.RenderOnlyLight)
        {
            Main.spriteBatch.Draw(
                worldTarget,
                Vector2.Zero,
                Color.White
            );
        }

        Main.spriteBatch.End();
    }
}
