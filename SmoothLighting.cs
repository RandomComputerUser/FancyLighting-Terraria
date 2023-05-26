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
    private byte[] _hasLight;
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
    private bool _smoothLightingForeComplete;
    private bool _smoothLightingBackComplete;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;
    private RenderTarget2D _cameraModeTarget3;

    internal bool DrawSmoothLightingBack
        => _smoothLightingBackComplete && LightingConfig.Instance.SmoothLightingEnabled();

    internal bool DrawSmoothLightingFore
        => _smoothLightingForeComplete && LightingConfig.Instance.SmoothLightingEnabled();

    private readonly FancyLightingMod _modInstance;

    private Shader _bicubicShader;
    private Shader _bicubicNoDitherHiDefShader;
    private Shader _noFilterShader;
    private Shader _qualityNormalsShader;
    private Shader _qualityNormalsOverbrightShader;
    private Shader _qualityNormalsOverbrightAmbientOcclusionShader;
    private Shader _qualityNormalsOverbrightLightOnlyHiDefShader;
    private Shader _normalsShader;
    private Shader _normalsOverbrightShader;
    private Shader _normalsOverbrightLightOnlyHiDefShader;
    private Shader _overbrightShader;
    private Shader _overbrightAmbientOcclusionShader;
    private Shader _overbrightLightOnlyHiDefShader;
    private Shader _overbrightMaxShader;
    private Shader _gammaCorrectionShader;
    private Shader _gammaCorrectionBGShader;

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
            TileID.Crystals, // Crystal Shards and Gelatin Crystal
            TileID.AshGrass,
            TileID.LavaMoss,
            TileID.LavaMossBrick,
            TileID.LavaMossBlock,
            TileID.ArgonMoss,
            TileID.ArgonMossBrick,
            TileID.ArgonMossBlock,
            TileID.KryptonMoss,
            TileID.KryptonMossBrick,
            TileID.KryptonMossBlock,
            TileID.XenonMoss,
            TileID.XenonMossBrick,
            TileID.XenonMossBlock,
            TileID.VioletMoss, // Neon Moss
            TileID.VioletMossBrick,
            TileID.VioletMossBlock,
            TileID.RainbowMoss,
            TileID.RainbowMossBrick,
            TileID.RainbowMossBlock,
            TileID.RainbowBrick,
            TileID.MeteoriteBrick,
            TileID.MartianConduitPlating,
            TileID.LihzahrdAltar,
            TileID.LunarMonolith,
            TileID.VoidMonolith,
            TileID.ShimmerMonolith, // Aether Monolith
            TileID.PixelBox,
            TileID.LavaLamp
        })
        {
            _glowingTiles[id] = true;
        }

        _glowingTileColors = new Color[_glowingTiles.Length];

        _glowingTileColors[TileID.Crystals] = Color.White;
        _glowingTileColors[TileID.AshGrass] = new(153, 66, 23);

        _glowingTileColors[TileID.LavaMoss]
            = _glowingTileColors[TileID.LavaMossBrick]
            = _glowingTileColors[TileID.LavaMossBlock]
            = new(225, 61, 0);
        _glowingTileColors[TileID.ArgonMoss]
            = _glowingTileColors[TileID.ArgonMossBrick]
            = _glowingTileColors[TileID.ArgonMossBlock]
            = new(255, 13, 129);
        _glowingTileColors[TileID.KryptonMoss]
            = _glowingTileColors[TileID.KryptonMossBrick]
            = _glowingTileColors[TileID.KryptonMossBlock]
            = new(20, 255, 0);
        _glowingTileColors[TileID.XenonMoss]
            = _glowingTileColors[TileID.XenonMossBrick]
            = _glowingTileColors[TileID.XenonMossBlock]
            = new(0, 227, 255);
        _glowingTileColors[TileID.VioletMoss] // Neon Moss
            = _glowingTileColors[TileID.VioletMossBrick]
            = _glowingTileColors[TileID.VioletMossBlock]
            = new(152, 6, 255);
        // Rainbow Moss and Bricks are handled separately

        _glowingTileColors[TileID.MeteoriteBrick] = new(219, 104, 19);
        // Martian Conduit Plating is handled separately

        _glowingTileColors[TileID.LihzahrdAltar] = new(138, 130, 22);
        _glowingTileColors[TileID.LunarMonolith] = new(192, 192, 192);
        _glowingTileColors[TileID.VoidMonolith] = new(161, 255, 223);
        _glowingTileColors[TileID.ShimmerMonolith] = new(213, 196, 252);
        _glowingTileColors[TileID.PixelBox] = new(255, 255, 255);
        _glowingTileColors[TileID.LavaLamp] = new(255, 90, 2);

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
            "FancyLighting/Effects/LightRendering",
            "QualityNormals",
            true
        );
        _qualityNormalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "QualityNormalsOverbright",
            true
        );
        _qualityNormalsOverbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "QualityNormalsOverbrightAmbientOcclusion",
            true
        );
        _qualityNormalsOverbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "QualityNormalsOverbrightLightOnlyHiDef"
        );
        _normalsShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Normals"
        );
        _normalsOverbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbright",
            true
        );
        _normalsOverbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "NormalsOverbrightLightOnlyHiDef"
        );
        _overbrightShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "Overbright",
            true
        );
        _overbrightAmbientOcclusionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightAmbientOcclusion",
            true
        );
        _overbrightLightOnlyHiDefShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightLightOnlyHiDef"
        );
        _overbrightMaxShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "OverbrightMax",
            true
        );
        _gammaCorrectionShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "GammaCorrection"
        );
        _gammaCorrectionBGShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/LightRendering",
            "GammaCorrectionBG"
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
        EffectLoader.UnloadEffect(ref _qualityNormalsOverbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _qualityNormalsOverbrightLightOnlyHiDefShader);
        EffectLoader.UnloadEffect(ref _normalsShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightShader);
        EffectLoader.UnloadEffect(ref _normalsOverbrightLightOnlyHiDefShader);
        EffectLoader.UnloadEffect(ref _overbrightShader);
        EffectLoader.UnloadEffect(ref _overbrightAmbientOcclusionShader);
        EffectLoader.UnloadEffect(ref _overbrightLightOnlyHiDefShader);
        EffectLoader.UnloadEffect(ref _overbrightMaxShader);
        EffectLoader.UnloadEffect(ref _gammaCorrectionShader);
        EffectLoader.UnloadEffect(ref _gammaCorrectionBGShader);
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

    internal void ApplyGammaCorrectionShader() => _gammaCorrectionShader.Apply();

    internal void ApplyGammaCorrectionBGShader() => _gammaCorrectionBGShader.Apply();

    internal void ApplyNoFilterShader() => _noFilterShader.Apply();

    internal void GetAndBlurLightMap(Vector3[] colors, int width, int height)
    {
        _smoothLightingLightMapValid = false;
        _smoothLightingPositionValid = false;
        _smoothLightingForeComplete = false;
        _smoothLightingBackComplete = false;

        if (_lights is null || _lights.Length < height * width)
        {
            _lights = new Vector3[height * width];
        }
        if (_whiteLights is null || _whiteLights.Length < height * width)
        {
            _whiteLights = new Vector3[height * width];
        }
        if (_hasLight is null || _hasLight.Length < height * width)
        {
            _hasLight = new byte[height * width];
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
        bool doGammaCorrection = LightingConfig.Instance.UseGammaCorrection();
        bool blurLightMap = LightingConfig.Instance.UseLightMapBlurring;

        if (doGammaCorrection)
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
                            GammaConverter.GammaToLinear(ref colors[i]);
                            ++i;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }
        }

        if (blurLightMap)
        {
            if (LightingConfig.Instance.UseBrighterBlurring)
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
                                ref Vector3 light = ref _lights[i];

                                // Faster to do it separately for each component
                                light.X = Math.Max(colors[i].X, (
                                      (colors[i - height - 1].X + 2f * colors[i - height].X + colors[i - height + 1].X)
                                    + 2f * (colors[i - 1].X + 2f * colors[i].X + colors[i + 1].X)
                                    + (colors[i + height - 1].X + 2f * colors[i + height].X + colors[i + height + 1].X)
                                ) * (1f / 16f));

                                light.Y = Math.Max(colors[i].Y, (
                                      (colors[i - height - 1].Y + 2f * colors[i - height].Y + colors[i - height + 1].Y)
                                    + 2f * (colors[i - 1].Y + 2f * colors[i].Y + colors[i + 1].Y)
                                    + (colors[i + height - 1].Y + 2f * colors[i + height].Y + colors[i + height + 1].Y)
                                ) * (1f / 16f));

                                light.Z = Math.Max(colors[i].Z, (
                                      (colors[i - height - 1].Z + 2f * colors[i - height].Z + colors[i - height + 1].Z)
                                    + 2f * (colors[i - 1].Z + 2f * colors[i].Z + colors[i + 1].Z)
                                    + (colors[i + height - 1].Z + 2f * colors[i + height].Z + colors[i + height + 1].Z)
                                ) * (1f / 16f));
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                                break;
                            }
                        }
                    }
                );
            }
            else
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
                                ref Vector3 light = ref _lights[i];

                                // Faster to do it separately for each component
                                light.X = (
                                      (colors[i - height - 1].X + 2f * colors[i - height].X + colors[i - height + 1].X)
                                    + 2f * (colors[i - 1].X + 2f * colors[i].X + colors[i + 1].X)
                                    + (colors[i + height - 1].X + 2f * colors[i + height].X + colors[i + height + 1].X)
                                ) * (1f / 16f);

                                light.Y = (
                                      (colors[i - height - 1].Y + 2f * colors[i - height].Y + colors[i - height + 1].Y)
                                    + 2f * (colors[i - 1].Y + 2f * colors[i].Y + colors[i + 1].Y)
                                    + (colors[i + height - 1].Y + 2f * colors[i + height].Y + colors[i + height + 1].Y)
                                ) * (1f / 16f);

                                light.Z = (
                                      (colors[i - height - 1].Z + 2f * colors[i - height].Z + colors[i - height + 1].Z)
                                    + 2f * (colors[i - 1].Z + 2f * colors[i].Z + colors[i + 1].Z)
                                    + (colors[i + height - 1].Z + 2f * colors[i + height].Z + colors[i + height + 1].Z)
                                ) * (1f / 16f);
                            }
                            catch (IndexOutOfRangeException)
                            {
                                Interlocked.Exchange(ref caughtException, 1);
                                break;
                            }
                        }
                    }
                );
            }

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
        }

        if (doGammaCorrection)
        {
            Vector3[] lightColors = blurLightMap ? _lights : colors;

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
                            GammaConverter.LinearToSrgb(ref lightColors[i++]);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Interlocked.Exchange(ref caughtException, 1);
                            break;
                        }
                    }
                }
            );

            if (caughtException == 1)
            {
                PrintException();
                return;
            }
        }

        if (blurLightMap)
        {
            Array.Copy(_lights, colors, height * width);
        }
        else
        {
            Array.Copy(colors, _lights, height * width);
        }

        LightingEngine lightEngine = (LightingEngine)_modInstance.field_activeEngine.GetValue(null);
        Rectangle lightMapTileArea = (Rectangle)_modInstance.field_workingProcessedArea.GetValue(lightEngine);

        _isDangersenseActive = Main.LocalPlayer.dangerSense;
        _isSpelunkerActive = Main.LocalPlayer.findTreasure;

        const float LOW = 0.25f / 255f;

        int ymax = lightMapTileArea.Y + lightMapTileArea.Height;
        Parallel.For(
            lightMapTileArea.X,
            lightMapTileArea.X + lightMapTileArea.Width,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (x) =>
            {
                bool isXInTilemap = x >= 0 && x < Main.tile.Width;
                ushort tilemapHeight = Main.tile.Height;
                int i = height * (x - lightMapTileArea.X);
                for (int y = lightMapTileArea.Y; y < ymax; ++y)
                {
                    try
                    {
                        ref Vector3 color = ref _lights[i];
                        if (color.X > LOW || color.Y > LOW || color.Z > LOW)
                        {
                            _hasLight[i++] = 2;
                            continue;
                        }

                        if (isXInTilemap && y >= 0 && y < tilemapHeight)
                        {
                            Tile tile = Main.tile[x, y];

                            if (tile.IsTileFullbright // Illuminant Paint
                                || tile.IsWallFullbright
                                || tile.LiquidType == LiquidID.Shimmer // Shimmer
                                || _glowingTiles[tile.TileType] // Glowing Tiles
                                || (
                                    _isDangersenseActive // Dangersense Potion
                                    && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer)
                                )
                                || (_isSpelunkerActive && Main.IsTileSpelunkable(x, y))) // Spelunker Potion
                            {
                                _hasLight[i++] = 2;
                                continue;
                            }
                        }

                        if (_hasLight[i] != 0)
                        {
                            --_hasLight[i];
                        }
                        ++i;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Interlocked.Exchange(ref caughtException, 1);
                        break;
                    }
                }
            }
        );

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        Parallel.For(
            1,
            width - 1,
            new ParallelOptions { MaxDegreeOfParallelism = LightingConfig.Instance.ThreadCount },
            (x) =>
            {
                int i = height * x;
                for (int y = 1; y < height - 1; ++y)
                {
                    try
                    {
                        ref Vector3 whiteLight = ref _whiteLights[++i];

                        if (_hasLight[i] != 0
                            || _hasLight[i - 1] != 0
                            || _hasLight[i + 1] != 0
                            || _hasLight[i - height] != 0
                            || _hasLight[i - height - 1] != 0
                            || _hasLight[i - height + 1] != 0
                            || _hasLight[i + height] != 0
                            || _hasLight[i + height - 1] != 0
                            || _hasLight[i + height + 1] != 0)
                        {
                            whiteLight.X = 1f;
                            whiteLight.Y = 1f;
                            whiteLight.Z = 1f;
                        }
                        else
                        {
                            whiteLight.X = LOW;
                            whiteLight.Y = LOW;
                            whiteLight.Z = LOW;
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Interlocked.Exchange(ref caughtException, 1);
                        break;
                    }
                }
            }
        );

        if (caughtException == 1)
        {
            PrintException();
            return;
        }

        _lightMapTileArea = lightMapTileArea;
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

    internal void CalculateSmoothLighting(bool background, bool cameraMode = false, bool force = false)
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            return;
        }

        if (!_smoothLightingLightMapValid)
        {
            return;
        }

        if (!force)
        {
            if (background && !cameraMode && _smoothLightingBackComplete)
            {
                return;
            }

            if (!background && !cameraMode && _smoothLightingForeComplete)
            {
                return;
            }
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
        float glowMult = brightness / 255f;
        float fullBrightness = brightness;
        float multFromOverbright;
        if (LightingConfig.Instance.DrawOverbright())
        {
            VectorToColor.Assign(ref whiteLight, brightness, new Vector3(overbrightMult));
            fullBrightness *= overbrightMult;
            multFromOverbright = overbrightMult;
        }
        else
        {
            multFromOverbright = 1f;
        }

        if (background)
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
                            Tile tile = Main.tile[x, y];

                            // Illuminant Paint
                            if (tile.IsWallFullbright)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            // Shimmer
                            if (tile.LiquidType == LiquidID.Shimmer)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            VectorToColor.Assign(ref _finalLightsHiDef[i], fullBrightness, _lights[i]);
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
        else
        {
            _glowingTileColors[TileID.MartianConduitPlating] = new Color(new Vector3(
                (float)(0.4 - 0.4 * Math.Cos(
                    (int)(0.08 * Main.timeForVisualEffects / 6.283) % 3 == 1
                        ? 0.08 * Main.timeForVisualEffects
                        : 0.0
                    )
                )
            ));

            _glowingTileColors[TileID.RainbowMoss]
                = _glowingTileColors[TileID.RainbowMossBrick]
                = _glowingTileColors[TileID.RainbowMossBlock]
                = _glowingTileColors[TileID.RainbowBrick]
                = Main.DiscoColor;

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
                            Tile tile = Main.tile[x, y];

                            // Illuminant Paint
                            if (tile.IsTileFullbright)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            // Shimmer
                            if (tile.LiquidType == LiquidID.Shimmer)
                            {
                                _finalLightsHiDef[i++] = whiteLight;
                                continue;
                            }

                            Vector3.Multiply(ref _lights[i], brightness, out Vector3 lightColor);

                            // Crystal Shards, Gelatin Crystal, Glowing Moss, Meteorite Brick, and Martian Conduit Plating
                            if (_glowingTiles[tile.TileType])
                            {
                                ref Color glow = ref _glowingTileColors[tile.TileType];

                                lightColor.X = Math.Max(lightColor.X, glowMult * glow.R);
                                lightColor.Y = Math.Max(lightColor.Y, glowMult * glow.G);
                                lightColor.Z = Math.Max(lightColor.Z, glowMult * glow.B);
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

                            VectorToColor.Assign(ref _finalLightsHiDef[i], multFromOverbright, lightColor);
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
        float glowMult = brightness / 255f;
        float fullBrightness = brightness;
        float multFromOverbright;
        if (LightingConfig.Instance.DrawOverbright())
        {
            whiteLight = new Color(new Vector3(overbrightMult * brightness));
            fullBrightness *= overbrightMult;
            multFromOverbright = overbrightMult;
        }
        else
        {
            whiteLight = Color.White;
            multFromOverbright = 1f;
        }

        if (background)
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
                            Tile tile = Main.tile[x, y];

                            // Illuminant Paint
                            if (tile.IsWallFullbright)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            // Shimmer
                            if (tile.LiquidType == LiquidID.Shimmer)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            VectorToColor.Assign(ref _finalLights[i], fullBrightness, _lights[i]);
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
        else
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
                            Tile tile = Main.tile[x, y];

                            // Illuminant Paint
                            if (tile.IsTileFullbright)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            // Shimmer
                            if (tile.LiquidType == LiquidID.Shimmer)
                            {
                                _finalLights[i++] = whiteLight;
                                continue;
                            }

                            Vector3.Multiply(ref _lights[i], brightness, out Vector3 lightColor);

                            // Glowing Tiles
                            if (_glowingTiles[tile.TileType])
                            {
                                ref Color glow = ref _glowingTileColors[tile.TileType];

                                lightColor.X = Math.Max(lightColor.X, glowMult * glow.R);
                                lightColor.Y = Math.Max(lightColor.Y, glowMult * glow.G);
                                lightColor.Z = Math.Max(lightColor.Z, glowMult * glow.B);
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

                            VectorToColor.Assign(ref _finalLights[i], multFromOverbright, lightColor);
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
        RenderTarget2D tempTarget = null,
        RenderTarget2D ambientOcclusionTarget = null
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
            doScaling,
            ambientOcclusionTarget
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
        bool disableNormalMaps = false,
        bool tileEntities = false,
        RenderTarget2D ambientOcclusionTarget = null
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
                tileEntities,
                ambientOcclusionTarget
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
        bool doScaling,
        RenderTarget2D ambientOcclusionTarget
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
        bool noDithering = ((simulateNormalMaps && qualityNormalMaps) || doOverbright) && hiDef;
        bool doGamma = LightingConfig.Instance.UseGammaCorrection();
        bool doAmbientOcclusion = background && ambientOcclusionTarget is not null;

        Main.instance.GraphicsDevice.SetRenderTarget(simulateNormalMaps || doOverbright ? target2 : target1);
        Main.instance.GraphicsDevice.Clear(Color.White);
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            FancyLightingMod.MultiplyBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        if (doBicubicUpscaling)
        {
            if (noDithering)
            {
                _bicubicNoDitherHiDefShader
                    .SetParameter("LightMapSize", lightMapTexture.Size())
                    .SetParameter("PixelSize", new Vector2(
                        1f / lightMapTexture.Width,
                        1f / lightMapTexture.Height))
                    .Apply();
            }
            else
            {
                _bicubicShader
                    .SetParameter("LightMapSize", lightMapTexture.Size())
                    .SetParameter("PixelSize", new Vector2(
                        1f / lightMapTexture.Width,
                        1f / lightMapTexture.Height))
                    .SetParameter("DitherCoordMult", new Vector2(
                        16f * lightMapTexture.Width / _ditherMask.Width,
                        16f * lightMapTexture.Height / _ditherMask.Height))
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

            Shader shader
            = simulateNormalMaps
                ? qualityNormalMaps
                    ? doOverbright
                        ? lightOnly
                            ? _qualityNormalsOverbrightLightOnlyHiDefShader
                            : doAmbientOcclusion
                                ? _qualityNormalsOverbrightAmbientOcclusionShader
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
                        : doAmbientOcclusion
                            ? _overbrightAmbientOcclusionShader
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
            float hiDefNormalMapStrength = background ? 0.44f : 0.9f;
            float hiDefNormalMapExp = background ? 1 / 3f : 1 / 2f;
            if (doGamma)
            {
                hiDefNormalMapStrength *= 1.4f;
            }

            shader
                .SetParameter("NormalMapResolution", new Vector2(
                    normalMapResolution / worldTarget.Width,
                    normalMapResolution / worldTarget.Height))
                .SetParameter("NormalMapRadius", new Vector2(
                    normalMapRadius / target2.Width,
                    normalMapRadius / target2.Height))
                .SetParameter("WorldCoordMult", new Vector2(
                    (float)target2.Width / worldTarget.Width,
                    (float)target2.Height / worldTarget.Height))
                .SetParameter("HiDefNormalMapStrength", hiDefNormalMapStrength)
                .SetParameter("HiDefNormalMapExp", hiDefNormalMapExp);
            Main.instance.GraphicsDevice.Textures[1] = worldTarget;
            Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;
            if (noDithering)
            {
                shader.SetParameter("DitherCoordMult", new Vector2(
                    (float)target2.Width / _ditherMask.Width,
                    (float)target2.Height / _ditherMask.Height));
                Main.instance.GraphicsDevice.Textures[2] = _ditherMask;
                Main.instance.GraphicsDevice.SamplerStates[2] = SamplerState.PointWrap;
            }
            if (doAmbientOcclusion)
            {
                shader.SetParameter("AmbientOcclusionCoordMult", new Vector2(
                    (float)target2.Width / ambientOcclusionTarget.Width,
                    (float)target2.Height / ambientOcclusionTarget.Height));
                Main.instance.GraphicsDevice.Textures[3] = ambientOcclusionTarget;
                Main.instance.GraphicsDevice.SamplerStates[3] = SamplerState.PointClamp;
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
