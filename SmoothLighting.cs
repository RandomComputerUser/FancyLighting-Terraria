using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Light;
using Terraria.Graphics.Shaders;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FancyLighting
{
    internal sealed class SmoothLighting
    {
        internal Texture2D _colors;
        internal Texture2D _colorsBackground;
        internal Vector2 _colorsPosition;
        internal Vector2 _colorsPass1Position;
        internal Vector2 _colorsPass2Position;
        internal Rectangle _lightMapTileArea;
        internal Rectangle _lightMapRenderArea;
        internal Rectangle _lightMapPass2RenderArea;
        internal RenderTarget2D _surface;
        internal RenderTarget2D _surface2;
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

        public SmoothLighting(FancyLightingMod mod) {
            _tileLightScannerInstance = new TileLightScanner();
            _modInstance = mod;

            _lightMapTileArea = new Rectangle(0, 0, 0, 0);
            _lightMapRenderArea = new Rectangle(0, 0, 0, 0);
            _lightMapPass2RenderArea = new Rectangle(0, 0, 0, 0);

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
            }) {
                _glowingTiles[id] = true;
            }

            _glowingTileColors = new Color[_glowingTiles.Length];

            _glowingTileColors[TileID.Crystals] = Color.White;

            _glowingTileColors[TileID.LavaMoss] = _glowingTileColors[TileID.LavaMossBrick]       = new Color(254, 122, 0);
            _glowingTileColors[TileID.ArgonMoss] = _glowingTileColors[TileID.ArgonMossBrick]     = new Color(254, 92, 186);
            _glowingTileColors[TileID.KryptonMoss] = _glowingTileColors[TileID.KryptonMossBrick] = new Color(215, 255, 0);
            _glowingTileColors[TileID.XenonMoss] = _glowingTileColors[TileID.XenonMossBrick]     = new Color(0, 254, 242);

            _glowingTileColors[TileID.MeteoriteBrick] = new Color(219, 104, 19);
            // Martian Conduit Plating is handled separately
            _glowingTileColors[TileID.LavaLamp] = new Color(255, 90, 2);

            _isDangersenseActive = false;
            _isSpelunkerActive = false;
            
            GameShaders.Misc["FancyLighting:UpscalingSmooth"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleSmooth"
                );

            GameShaders.Misc["FancyLighting:UpscalingRegular"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/Upscaling", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "UpscaleNoFilter"
                );

            _printExceptionTime = 0;
        }

        internal void Unload()
        {
            _surface?.Dispose();
            _surface2?.Dispose();
            _colors?.Dispose();
            _colorsBackground?.Dispose();
            GameShaders.Misc["FancyLighting:UpscalingSmooth"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:UpscalingSmooth");
            GameShaders.Misc["FancyLighting:UpscalingRegular"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:UpscalingRegular");
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

            if (width == 0 || height == 0) return;
            if (colors.Length < height * width) return;

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
                                    + 2f * colors[i - height].X     + 4f * colors[i].X     + 2f * colors[i + height].X
                                    + 1f * colors[i - height + 1].X + 2f * colors[i + 1].X + 1f * colors[i + height + 1].X
                                ) / 16f;

                                _lights[i].Y = (
                                      1f * colors[i - height - 1].Y + 2f * colors[i - 1].Y + 1f * colors[i + height - 1].Y
                                    + 2f * colors[i - height].Y     + 4f * colors[i].Y     + 2f * colors[i + height].Y
                                    + 1f * colors[i - height + 1].Y + 2f * colors[i + 1].Y + 1f * colors[i + height + 1].Y
                                ) / 16f;

                                _lights[i].Z = (
                                      1f * colors[i - height - 1].Z + 2f * colors[i - 1].Z + 1f * colors[i + height - 1].Z
                                    + 2f * colors[i - height].Z     + 4f * colors[i].Z     + 2f * colors[i + height].Z
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

            LightingEngine lightEngine = (LightingEngine)_modInstance.field_activeEngine.GetValue(null);
            _lightMapTileArea = (Rectangle)_modInstance.field_workingProcessedArea.GetValue(lightEngine);
            _lightMapRenderArea = new Rectangle(0, 0, _lightMapTileArea.Height, _lightMapTileArea.Width);
            _lightMapPass2RenderArea = new Rectangle(0, 0, 16 * _lightMapTileArea.Width, 16 * _lightMapTileArea.Height);

            _smoothLightingLightMapValid = true;
        }

        private void GetColorsPosition()
        {
            int xmin = _lightMapTileArea.X;
            int ymin = _lightMapTileArea.Y;
            int width = _lightMapTileArea.Width;
            int height = _lightMapTileArea.Height;

            if (width == 0 || height == 0) return;

            _colorsPosition = 16f * new Vector2(xmin + width, ymin);
            _colorsPass1Position = 16f * new Vector2(width, 0);
            _colorsPass2Position = 16f * new Vector2(xmin, ymin);

            _smoothLightingPositionValid = true;
        }

        internal void CalculateSmoothLighting(bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled)
                return;
            if (!_smoothLightingLightMapValid)
                return;

            _isDangersenseActive = Main.LocalPlayer.dangerSense;
            _isSpelunkerActive = Main.LocalPlayer.findTreasure;

            if (!_smoothLightingPositionValid)
                GetColorsPosition();

            if (!_smoothLightingPositionValid)
                return;
            if (Main.tile.Height == 0 || Main.tile.Width == 0)
                return;

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
                return;
            int clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
            int clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
            if (clampedEnd - clampedStart < 1)
                return;

            int clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
            int clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
            if (clampedYmax - clampedYmin < 1)
                return;
            int offset = clampedYmin - ymin;
            if (offset < 0 || offset >= height)
                return;

            int caughtException = 0;

            if (background && !_smoothLightingBackComplete)
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
                                    _finalLights[i++] = Color.White;
                                    continue;
                                }

                                _finalLights[i] = new Color(Lighting.GlobalBrightness * _lights[i]);
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

                if (_colorsBackground is null
                    || _colorsBackground.GraphicsDevice != Main.graphics.GraphicsDevice
                    || _colorsBackground.Width < height
                    || _colorsBackground.Height < width)
                {
                    _colorsBackground?.Dispose();
                    _colorsBackground = new Texture2D(
                        Main.graphics.GraphicsDevice,
                        Math.Max(height, _colorsBackground?.Width ?? 0),
                        Math.Max(width, _colorsBackground?.Height ?? 0),
                        false,
                        SurfaceFormat.Color
                    );
                }

                _colorsBackground.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

                _smoothLightingBackComplete = true;
            }
            else if (!_smoothLightingForeComplete)
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
                                    _finalLights[i++] = Color.White;
                                    continue;
                                }

                                _finalLights[i] = new Color(Lighting.GlobalBrightness * _lights[i]);

                                // Crystal Shards, Gelatin Crystal, Glowing Moss, and Meteorite Brick
                                if (_glowingTiles[Main.tile[x, y].TileType])
                                {
                                    ref Color glow = ref _glowingTileColors[Main.tile[x, y].TileType];
                                    if (_finalLights[i].R < glow.R) _finalLights[i].R = glow.R;
                                    if (_finalLights[i].G < glow.G) _finalLights[i].G = glow.G;
                                    if (_finalLights[i].B < glow.B) _finalLights[i].B = glow.B;
                                }

                                // Dangersense Potion
                                else if (_isDangersenseActive && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                                {
                                    if (_finalLights[i].R < (byte)255) _finalLights[i].R = (byte)255;
                                    if (_finalLights[i].G < (byte)50) _finalLights[i].G = (byte)50;
                                    if (_finalLights[i].B < (byte)50) _finalLights[i].B = (byte)50;
                                }

                                // Spelunker Potion
                                else if (_isSpelunkerActive && Main.IsTileSpelunkable(x, y))
                                {
                                    if (_finalLights[i].R < (byte)200) _finalLights[i].R = (byte)200;
                                    if (_finalLights[i].G < (byte)170) _finalLights[i].G = (byte)170;
                                }
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

                if (_colors is null
                    || _colors.GraphicsDevice != Main.graphics.GraphicsDevice
                    || _colors.Width < height
                    || _colors.Height < width)
                {
                    _colors?.Dispose();
                    _colors = new Texture2D(
                        Main.graphics.GraphicsDevice,
                        Math.Max(height, _colors?.Width ?? 0),
                        Math.Max(width, _colors?.Height ?? 0),
                        false,
                        SurfaceFormat.Color
                    );
                }

                _colors.SetData(0, _lightMapRenderArea, _finalLights, 0, height * width);

                _smoothLightingForeComplete = true;
            }
        }

        internal void DrawSmoothLighting(RenderTarget2D target, bool background, bool endSpriteBatch = true)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;
            if (!background && !_smoothLightingForeComplete) return;
            if (background && !_smoothLightingBackComplete) return;

            if (_surface is null
                || _surface.GraphicsDevice != Main.graphics.GraphicsDevice
                || _surface.Width != Main.instance.tileTarget.Width
                || _surface.Height != Main.instance.tileTarget.Height)
            {
                _surface?.Dispose();
                _surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }

            Texture2D lightMapTexture = background ? _colorsBackground : _colors;

            if (FancyLightingMod.CustomUpscalingEnabled)
            {
                if (_surface2 is null
                    || _surface2.GraphicsDevice != Main.graphics.GraphicsDevice
                    || _surface2.Width < 16f * lightMapTexture.Height
                    || _surface2.Height < 16f * lightMapTexture.Width)
                {
                    _surface2?.Dispose();
                    _surface2 = new RenderTarget2D(
                        Main.graphics.GraphicsDevice,
                        Math.Max(16 * lightMapTexture.Height, _surface2?.Width ?? 0),
                        Math.Max(16 * lightMapTexture.Width, _surface2?.Height ?? 0)
                    );
                }

                Main.instance.GraphicsDevice.SetRenderTarget(_surface2);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                GameShaders.Misc["FancyLighting:UpscalingSmooth"]
                    .UseShaderSpecificData(new Vector4(0.5f / lightMapTexture.Width, lightMapTexture.Width, 1f / lightMapTexture.Width, 0f))
                    .Apply(null);
                Main.spriteBatch.Draw(
                    lightMapTexture,
                    _colorsPass1Position,
                    _lightMapRenderArea,
                    Color.White,
                    (float)(Math.PI / 2.0),
                    Vector2.Zero,
                    16f,
                    SpriteEffects.FlipVertically,
                    0f
                );

                Main.spriteBatch.End();

                Main.instance.GraphicsDevice.SetRenderTarget(_surface);
                Main.instance.GraphicsDevice.Clear(Color.White);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    FancyLightingMod.MultiplyBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                GameShaders.Misc["FancyLighting:UpscalingSmooth"]
                    .UseShaderSpecificData(new Vector4(0.5f / lightMapTexture.Height, lightMapTexture.Height, 1f / lightMapTexture.Height, 0f))
                    .Apply(null);
                Main.spriteBatch.Draw(
                    _surface2,
                    _colorsPass2Position - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                    _lightMapPass2RenderArea,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    0f
                );
                GameShaders.Misc["FancyLighting:UpscalingRegular"]
                    .Apply(null);
            }
            else
            {
                Main.instance.GraphicsDevice.SetRenderTarget(_surface);
                Main.instance.GraphicsDevice.Clear(Color.White);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    FancyLightingMod.MultiplyBlend
                );
                Main.spriteBatch.Draw(
                    lightMapTexture,
                    _colorsPosition - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                    _lightMapRenderArea,
                    Color.White,
                    (float)(Math.PI / 2.0),
                    Vector2.Zero,
                    16f,
                    SpriteEffects.FlipVertically,
                    0f
                );
            }

            if (!FancyLightingMod.RenderOnlyLight)
            {
                Main.spriteBatch.Draw(
                    target,
                    Vector2.Zero,
                    null,
                    Color.White,
                    0f,
                    new Vector2(0, 0),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(target);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();
            Main.spriteBatch.Draw(
                _surface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.SetRenderTarget(null);
        }
    }
}
