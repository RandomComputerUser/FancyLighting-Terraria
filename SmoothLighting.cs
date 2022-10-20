using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting
{
    internal sealed class SmoothLighting
    {
        internal Texture2D _colors;
        internal Texture2D _colorsBackground;

        private Texture2D _ditherMask;

        internal Vector2 _lightMapPosition;
        internal Vector2 _lightMapPositionFlipped;
        internal Rectangle _lightMapTileArea;
        internal Rectangle _lightMapRenderArea;

        internal RenderTarget2D _drawTarget1;
        internal RenderTarget2D _drawTarget2;

        internal Vector3[] _lights;
        internal Vector3[] _whiteLights;
        internal Vector3[] _tmpLights;
        internal Color[] _finalLights;

        private bool[] _glowingTiles;
        private Color[] _glowingTileColors;

        private bool _isDangersenseActive;
        private bool _isSpelunkerActive;

        private bool _smoothLightingLightMapValid;
        private bool _smoothLightingPositionValid;
        private bool _smoothLightingBackComplete;
        private bool _smoothLightingForeComplete;

        internal RenderTarget2D _cameraModeTarget1;
        internal RenderTarget2D _cameraModeTarget2;
        internal RenderTarget2D _cameraModeTarget3;

        internal bool DrawSmoothLightingBack
        {
            get
            {
                return _smoothLightingBackComplete && FancyLightingMod.SmoothLightingEnabled;
            }
        }

        internal bool DrawSmoothLightingFore
        {
            get
            {
                return _smoothLightingForeComplete && FancyLightingMod.SmoothLightingEnabled;
            }
        }

        internal TileLightScanner _tileLightScannerInstance;

        private FancyLightingMod _modInstance;

        internal uint _printExceptionTime;

        public SmoothLighting(FancyLightingMod mod)
        {
            _tileLightScannerInstance = new TileLightScanner();
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

            GameShaders.Misc["FancyLighting:UpscaleBicubic"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleBicubic"
                );

            GameShaders.Misc["FancyLighting:UpscaleNoFilter"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleNoFilter"
                );

            GameShaders.Misc["FancyLighting:SimulateNormals"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/NormalMap", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "SimulateNormals"
                );

            GameShaders.Misc["FancyLighting:SimulateNormalsOverbright"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/NormalMap", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "SimulateNormalsOverbright"
                );

            GameShaders.Misc["FancyLighting:Overbright"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/NormalMap", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "Overbright"
                );

            GameShaders.Misc["FancyLighting:OverbrightMax"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/NormalMap", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "OverbrightMax"
                );

            _ditherMask = ModContent.Request<Texture2D>("FancyLighting/Effects/DitheringMask", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

            _printExceptionTime = 0;
        }

        internal void Unload()
        {
            _drawTarget1?.Dispose();
            _drawTarget2?.Dispose();
            _colors?.Dispose();
            _colorsBackground?.Dispose();
            _cameraModeTarget1?.Dispose();
            _cameraModeTarget2?.Dispose();
            _cameraModeTarget3?.Dispose();
            _ditherMask?.Dispose();
            GameShaders.Misc["FancyLighting:UpscaleBicubic"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:UpscaleBicubic");
            GameShaders.Misc["FancyLighting:UpscaleNoFilter"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:UpscaleNoFilter");
            GameShaders.Misc["FancyLighting:SimulateNormals"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:SimulateNormals");
            GameShaders.Misc["FancyLighting:SimulateNormalsOverbright"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:SimulateNormalsOverbright");
            GameShaders.Misc["FancyLighting:Overbright"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:Overbright");
            GameShaders.Misc["FancyLighting:OverbrightMax"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:OverbrightMax");
        }

        private void PrintException()
        {
            if (Main.GameUpdateCount >= _printExceptionTime)
            {
                Main.NewText("[Fancy Lighting] Caught an IndexOutOfRangeException; smooth lighting will be skipped this time.", Color.Orange);
                Main.NewText("[Fancy Lighting] Disable smooth lighting if this message repeatedly appears.", Color.Orange);
                _printExceptionTime = Main.GameUpdateCount + 5 * 60;
            }
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

            if (FancyLightingMod.BlurLightMap)
            {
                Parallel.For(
                    1,
                    width - 1,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
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

            if (FancyLightingMod.SimulateNormalMaps)
            {
                Parallel.For(
                    1,
                    width - 1,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
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
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x) =>
                    {
                        int i = height * x;
                        for (int y = 0; y < height; ++y)
                        {
                            try
                            {
                                ref Vector3 color = ref _lights[i];
                                if (color.X < 1f / 255f && color.Y < 1f / 255f && color.Z < 1f / 255f)
                                {
                                    _whiteLights[i++] = new Vector3(1f / 255f, 1f / 255f, 1f / 255f);
                                }
                                else
                                {
                                    _whiteLights[i++] = Vector3.One;
                                }
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
            if (!FancyLightingMod.SmoothLightingEnabled)
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

            if (_finalLights is null || _finalLights.Length < height * width)
            {
                _finalLights = new Color[height * width];
            }

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

            int caughtException = 0;

            const int overbrightWhite = 128;
            const float overbrightMult = overbrightWhite / 255f;

            Color whiteLight;
            float brightness = Lighting.GlobalBrightness;
            float fullBrightness = brightness;
            float multFromOverbright;
            if (FancyLightingMod.DrawOverbright)
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
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
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

                                _finalLights[i] = new Color(fullBrightness * _lights[i]);
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

                Textures.MakeAtLeastSize(ref _colorsBackground, height, width);

                _colorsBackground.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

                _smoothLightingBackComplete = !cameraMode;
            }
            else if (!background && (!_smoothLightingForeComplete || cameraMode))
            {
                _glowingTileColors[TileID.MartianConduitPlating] = new Color(new Vector3(
                    (float)(0.4 - 0.4 * Math.Cos((int)(0.08 * Main.timeForVisualEffects / 6.283) % 3 == 1 ? 0.08 * Main.timeForVisualEffects : 0.0))
                ));

                Parallel.For(
                    clampedStart,
                    clampedEnd,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
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

                                // Crystal Shards, Gelatin Crystal, Glowing Moss, and Meteorite Brick
                                if (_glowingTiles[Main.tile[x, y].TileType])
                                {
                                    ref Color glow = ref _glowingTileColors[Main.tile[x, y].TileType];
                                    if (lightColor.X < glow.R)
                                    {
                                        lightColor.X = glow.R / 255f;
                                    }

                                    if (lightColor.Y < glow.G)
                                    {
                                        lightColor.Y = glow.G / 255f;
                                    }

                                    if (lightColor.Z < glow.B)
                                    {
                                        lightColor.Z = glow.B / 255f;
                                    }
                                }

                                // Dangersense Potion
                                else if (_isDangersenseActive && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                                {
                                    if (lightColor.X < 255f / 255f)
                                    {
                                        lightColor.X = 255f / 255f;
                                    }

                                    if (lightColor.Y < 50f / 255f)
                                    {
                                        lightColor.Y = 50f / 255f;
                                    }

                                    if (lightColor.Z < 50f / 255f)
                                    {
                                        lightColor.Z = 50f / 255f;
                                    }
                                }

                                // Spelunker Potion
                                else if (_isSpelunkerActive && Main.IsTileSpelunkable(x, y))
                                {
                                    if (lightColor.X < 200f / 255f)
                                    {
                                        lightColor.X = 200f / 255f;
                                    }

                                    if (lightColor.Y < 170f / 255f)
                                    {
                                        lightColor.Y = 170f / 255f;
                                    }
                                }

                                _finalLights[i] = new Color(multFromOverbright * lightColor);
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

                Textures.MakeAtLeastSize(ref _colors, height, width);

                _colors.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

                _smoothLightingForeComplete = !cameraMode;
            }
        }

        internal void DrawSmoothLighting(
            RenderTarget2D target,
            bool background,
            bool disableNormalMaps = false,
            RenderTarget2D tempTarget = null)
        {
            if (!FancyLightingMod.SmoothLightingEnabled)
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
                Textures.MakeSize(ref _drawTarget1, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
                tempTarget = _drawTarget1;
                offset = new Vector2(Main.offScreenRange);
            }
            else
            {
                offset = (tempTarget.Size() - new Vector2(Main.screenWidth, Main.screenHeight)) / 2f;
            }

            Texture2D lightMapTexture = background ? _colorsBackground : _colors;

            if (FancyLightingMod.SimulateNormalMaps || FancyLightingMod.DrawOverbright)
            {
                Textures.MakeAtLeastSize(ref _drawTarget2, 16 * lightMapTexture.Height, 16 * lightMapTexture.Width);
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
            Textures.MakeSize(ref _cameraModeTarget1, screenTarget.Width, screenTarget.Height);
            return _cameraModeTarget1;
        }

        internal void DrawSmoothLightingCameraMode(
            RenderTarget2D screenTarget,
            RenderTarget2D target,
            bool background,
            bool skipFinalPass = false,
            bool disableNormalMaps = false)
        {
            Texture2D lightMapTexture = background ? _colorsBackground : _colors;

            Textures.MakeAtLeastSize(ref _cameraModeTarget2, 16 * lightMapTexture.Height, 16 * lightMapTexture.Width);
            Textures.MakeAtLeastSize(ref _cameraModeTarget3, 16 * lightMapTexture.Height, 16 * lightMapTexture.Width);

            if (FancyLightingMod.SmoothLightingEnabled)
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
            bool doScaling)
        {
            bool simulateNormalMaps = !disableNormalMaps && FancyLightingMod.SimulateNormalMaps;
            bool doBicubicUpscaling = FancyLightingMod.UseBicubicScaling;
            bool doOverbright = FancyLightingMod.DrawOverbright && !FancyLightingMod.RenderOnlyLight;

            Main.instance.GraphicsDevice.SetRenderTarget(simulateNormalMaps || doOverbright ? target2 : target1);
            Main.instance.GraphicsDevice.Clear(Color.White);
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend
            );

            if (doBicubicUpscaling)
            {
                GameShaders.Misc["FancyLighting:UpscaleBicubic"]
                    .UseShaderSpecificData(new Vector4(
                        1f / lightMapTexture.Width,
                        1f / lightMapTexture.Height,
                        lightMapTexture.Width,
                        lightMapTexture.Height))
                    .UseColor(16f * lightMapTexture.Width / _ditherMask.Width, 16f * lightMapTexture.Height / _ditherMask.Height, 0f)
                    .Apply(null);
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
                lightMapPosition.Y -= (bottom - targetMiddle) - (targetMiddle - top);
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

                string shader;
                if (simulateNormalMaps && doOverbright)
                {
                    shader = "FancyLighting:SimulateNormalsOverbright";
                }
                else if (simulateNormalMaps)
                {
                    shader = "FancyLighting:SimulateNormals";
                }
                else if (doOverbright && doScaling)
                {
                    shader = "FancyLighting:OverbrightMax";
                }
                else
                {
                    shader = "FancyLighting:Overbright";
                }

                float normalMapRadius = background ? 60f : 40f;
                normalMapRadius *= FancyLightingMod.NormalMapsStrength;

                GameShaders.Misc[shader]
                    .UseShaderSpecificData(new Vector4(
                        1f / worldTarget.Width,
                        1f / worldTarget.Height,
                        normalMapRadius / target2.Width,
                        normalMapRadius / target2.Height))
                    .UseColor((float)target2.Width / worldTarget.Width, (float)target2.Height / worldTarget.Height, 0f)
                    .Apply(null);
                Main.instance.GraphicsDevice.Textures[1] = worldTarget;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

                Main.spriteBatch.Draw(
                    target2,
                    Vector2.Zero,
                    Color.White
                );
            }

            if (doBicubicUpscaling || simulateNormalMaps)
            {
                GameShaders.Misc["FancyLighting:UpscaleNoFilter"]
                    .Apply(null);
            }

            if (!doOverbright && !FancyLightingMod.RenderOnlyLight)
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
}
