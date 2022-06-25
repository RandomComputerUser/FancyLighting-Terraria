using Terraria;
using Terraria.Graphics.Light;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Threading.Tasks;

namespace FancyLighting
{
    class SmoothLighting
    {

        internal Texture2D colors;
        internal Texture2D colorsBackground;
        internal Vector2 colorsPosition;
        internal Rectangle lightMapRenderArea;
        internal Rectangle lightMapTileArea;
        internal RenderTarget2D surface;
        internal Vector3[] lights;
        internal Color[] finalLights;

        private bool _smoothLightingPositionValid;
        private bool _smoothLightingBackComplete;
        private bool _smoothLightingForeComplete;

        internal TileLightScanner TileLightScannerObj;

        protected FancyLightingMod ModInstance;

        public SmoothLighting(FancyLightingMod mod) {
            TileLightScannerObj = new TileLightScanner();
            ModInstance = mod;

            lightMapTileArea = new Rectangle(0, 0, 0, 0);
            lightMapRenderArea = new Rectangle(0, 0, 0, 0);

            _smoothLightingPositionValid = false;
        }

        internal static bool IsGlowingTile(int x, int y)
        {
            if (x < 0 || x >= Main.tile.Width || y < 0 || y >= Main.tile.Height) return false;

            // Illuminant Paint
            if (Main.tile[x, y].TileColor == (byte)31 || Main.tile[x, y].WallColor == (byte)31)
                return true;

            // Crystal Shards and Gelatin Crystal, Meteorite Brick
            if (Main.tile[x, y].TileType == 129 || Main.tile[x, y].TileType == 370)
                return true;

            // Dangersense Potion
            if (Main.LocalPlayer.dangerSense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                return true;

            // Spelunker Potion
            if (Main.LocalPlayer.findTreasure && Main.IsTileSpelunkable(x, y))
                return true;

            return false;
        }

        internal void BlurLightMap(Vector3[] colors, int width, int height)
        {
            if (lights is null || lights.Length < height * width)
            {
                lights = new Vector3[height * width];
            }

            if (width == 0 || height == 0) return;

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

                        lights[i].X = (
                            1 * colors[i - height - 1].X + 2 * colors[i - 1].X + 1 * colors[i + height - 1].X
                          + 2 * colors[i - height].X + 4 * colors[i].X + 2 * colors[i + height].X
                          + 1 * colors[i - height + 1].X + 2 * colors[i + 1].X + 1 * colors[i + height + 1].X
                        ) / 16f;

                        lights[i].Y = (
                            1 * colors[i - height - 1].Y + 2 * colors[i - 1].Y + 1 * colors[i + height - 1].Y
                          + 2 * colors[i - height].Y + 4 * colors[i].Y + 2 * colors[i + height].Y
                          + 1 * colors[i - height + 1].Y + 2 * colors[i + 1].Y + 1 * colors[i + height + 1].Y
                        ) / 16f;

                        lights[i].Z = (
                            1 * colors[i - height - 1].Z + 2 * colors[i - 1].Z + 1 * colors[i + height - 1].Z
                          + 2 * colors[i - height].Z + 4 * colors[i].Z + 2 * colors[i + height].Z
                          + 1 * colors[i - height + 1].Z + 2 * colors[i + 1].Z + 1 * colors[i + height + 1].Z
                        ) / 16f;
                    }
                }
            );

            int offset = (width - 1) * height;
            for (int i = 0; i < height; ++i)
            {
                lights[i] = colors[i];
                lights[i + offset] = colors[i + offset];
            }

            int end = (width - 1) * height;
            offset = height - 1;
            for (int i = height; i < end; i += height)
            {
                lights[i] = colors[i];
                lights[i + offset] = colors[i + offset];
            }

            Array.Copy(lights, colors, height * width);

            LightingEngine lightEngine = (LightingEngine)ModInstance.field_activeEngine.GetValue(null);
            lightMapTileArea = (Rectangle)ModInstance.field_workingProcessedArea.GetValue(lightEngine);
            lightMapRenderArea = new Rectangle(0, 0, lightMapTileArea.Height, lightMapTileArea.Width);

            _smoothLightingPositionValid = false;
            _smoothLightingBackComplete = false;
            _smoothLightingForeComplete = false;
        }

        protected void GetLightingPosition()
        {
            int xmin = lightMapTileArea.X;
            int ymin = lightMapTileArea.Y;
            int width = lightMapTileArea.Width;
            int height = lightMapTileArea.Height;

            if (width == 0 || height == 0) return;

            colorsPosition = 16f * new Vector2(xmin + width, ymin);

            _smoothLightingPositionValid = true;
        }

        internal void CalculateSmoothLighting(bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;

            if (!_smoothLightingPositionValid)
                GetLightingPosition();

            if (!_smoothLightingPositionValid) return;
            if (Main.tile.Height == 0 || Main.tile.Width == 0) return;

            int xmin = lightMapTileArea.X;
            int ymin = lightMapTileArea.Y;
            int width = lightMapTileArea.Width;
            int height = lightMapTileArea.Height;
            int ymax = ymin + height;

            if (finalLights is null || finalLights.Length < height * width)
            {
                finalLights = new Color[height * width];
            }

            int clampedXmin = Math.Clamp(xmin, 0, Main.tile.Width);
            int clampedXmax = Math.Clamp(xmin + width, 0, Main.tile.Width);
            if (clampedXmax - clampedXmin < 1) return;
            int clampedStart = Math.Clamp(clampedXmin - xmin, 0, width);
            int clampedEnd = Math.Clamp(clampedXmax - clampedXmin, 0, width);
            if (clampedEnd - clampedStart < 1) return;

            int clampedYmin = Math.Clamp(ymin, 0, Main.tile.Height);
            int clampedYmax = Math.Clamp(ymax, 0, Main.tile.Height);
            if (clampedYmax - clampedYmin < 1) return;
            int offset = clampedYmin - ymin;
            if (offset < 0 || offset >= height) return;

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
                            // Also see: internal static bool IsGlowingTile(int x, int y)

                            // Illuminant Paint
                            if (Main.tile[x, y].WallColor == (byte)31)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            finalLights[i] = new Color(Lighting.GlobalBrightness * lights[i]);
                            ++i;
                        }
                    }
                );

                if (colorsBackground is null)
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, height, width, false, SurfaceFormat.Color);
                else if (colorsBackground.GraphicsDevice != Main.graphics.GraphicsDevice || (colorsBackground.Width < height || colorsBackground.Height < width))
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, Math.Max(colorsBackground.Width, height), Math.Max(colorsBackground.Height, width), false, SurfaceFormat.Color);

                colorsBackground.SetData(0, lightMapRenderArea, finalLights, 0, height * width);

                _smoothLightingBackComplete = true;
            }
            else if (!_smoothLightingForeComplete)
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
                            // Also see: internal static bool IsGlowingTile(int x, int y)

                            // Illuminant Paint
                            if (Main.tile[x, y].TileColor == (byte)31)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            // Crystal Shards and Gelatin Crystal
                            if (Main.tile[x, y].TileType == 129)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            finalLights[i] = new Color(Lighting.GlobalBrightness * lights[i]);

                            // Meteorite Brick
                            if (Main.tile[x, y].TileType == 370)
                            {
                                if (finalLights[i].R < (byte)219) finalLights[i].R = (byte)219;
                                if (finalLights[i].G < (byte)219) finalLights[i].G = (byte)104;
                                if (finalLights[i].B < (byte)219) finalLights[i].B = (byte)19;
                            }

                            // Dangersense Potion
                            else if (Main.LocalPlayer.dangerSense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                            {
                                if (finalLights[i].R < (byte)255) finalLights[i].R = (byte)255;
                                if (finalLights[i].G < (byte)50) finalLights[i].G = (byte)50;
                                if (finalLights[i].B < (byte)50) finalLights[i].B = (byte)50;
                            }

                            // Spelunker Potion
                            else if (Main.LocalPlayer.findTreasure && Main.IsTileSpelunkable(x, y))
                            {
                                if (finalLights[i].R < (byte)200) finalLights[i].R = (byte)200;
                                if (finalLights[i].G < (byte)170) finalLights[i].G = (byte)170;
                            }

                            ++i;
                        }
                    }
                );
                
                if (colors is null)
                    colors = new Texture2D(Main.graphics.GraphicsDevice, height, width, false, SurfaceFormat.Color);
                else if (colors.GraphicsDevice != Main.graphics.GraphicsDevice || (colors.Width < height || colors.Height < width))
                    colors = new Texture2D(Main.graphics.GraphicsDevice, Math.Max(colors.Width, height), Math.Max(colors.Height, width), false, SurfaceFormat.Color);

                colors.SetData(0, lightMapRenderArea, finalLights, 0, height * width);

                _smoothLightingForeComplete = true;
            }
        }

        internal void DrawSmoothLighting(RenderTarget2D target, bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;
            if (!background && !_smoothLightingForeComplete) return;
            if (background && !_smoothLightingBackComplete) return;

            if (surface is null || surface.GraphicsDevice != Main.graphics.GraphicsDevice || (surface.Width != Main.instance.tileTarget.Width || surface.Height != Main.instance.tileTarget.Height))
            {
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }

            // Main.instance.GraphicsDevice set its render target to null, so we need to switch to surface to do blending
            Main.instance.GraphicsDevice.SetRenderTarget(surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin();

            Main.spriteBatch.Draw(
                background ? colorsBackground : colors,
                colorsPosition - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                lightMapRenderArea,
                Color.White,
                (float)(Math.PI / 2.0),
                Vector2.Zero,
                16f,
                SpriteEffects.FlipVertically,
                0f
            );
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend
            );
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
            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(target);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();
            Main.spriteBatch.Draw(
                surface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.SetRenderTarget(null);
        }

    }
}
