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
        internal Vector3[] lightsPostBlur;
        internal Color[] finalLights;

        internal TileLightScanner TileLightScannerObj;

        protected FancyLightingMod ModInstance;

        public SmoothLighting(FancyLightingMod mod) {
            TileLightScannerObj = new TileLightScanner();
            ModInstance = mod;

        }

        internal static bool IsGlowingTile(int x, int y)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) return false;

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

        protected void GetBlurredLighting()
        {
            LightingEngine lightEngine = (LightingEngine)ModInstance.field_activeEngine.GetValue(null);
            Rectangle lightingArea = (Rectangle)ModInstance.field_activeProcessedArea.GetValue(lightEngine);
            Vector3[] lighting = (Vector3[])ModInstance.field_colors.GetValue((LightMap)ModInstance.field_activeLightMap.GetValue(lightEngine));

            int xmin = lightingArea.X;
            int ymin = lightingArea.Y;
            int width = lightingArea.Width;
            int height = lightingArea.Height;
            int ymax = ymin + height;

            if (width == 0 || height == 0) return;

            if (lights is null || lights.Length < height * width)
            {
                lights = new Vector3[height * width];
            }

            if (lightsPostBlur is null || lightsPostBlur.Length < height * width)
            {
                lightsPostBlur = new Vector3[height * width];
            }

            Array.Copy(lighting, lights, height * width);

            float multiplier = Lighting.GlobalBrightness / 16f;
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

                        lightsPostBlur[i].X = multiplier * (
                            1 * lights[i - height - 1].X + 2 * lights[i - 1].X + 1 * lights[i + height - 1].X
                          + 2 * lights[i - height].X     + 4 * lights[i].X     + 2 * lights[i + height].X
                          + 1 * lights[i - height + 1].X + 2 * lights[i + 1].X + 1 * lights[i + height + 1].X
                        );

                        lightsPostBlur[i].Y = multiplier * (
                            1 * lights[i - height - 1].Y + 2 * lights[i - 1].Y + 1 * lights[i + height - 1].Y
                          + 2 * lights[i - height].Y     + 4 * lights[i].Y     + 2 * lights[i + height].Y
                          + 1 * lights[i - height + 1].Y + 2 * lights[i + 1].Y + 1 * lights[i + height + 1].Y
                        );

                        lightsPostBlur[i].Z = multiplier * (
                            1 * lights[i - height - 1].Z + 2 * lights[i - 1].Z + 1 * lights[i + height - 1].Z
                          + 2 * lights[i - height].Z     + 4 * lights[i].Z     + 2 * lights[i + height].Z
                          + 1 * lights[i - height + 1].Z + 2 * lights[i + 1].Z + 1 * lights[i + height + 1].Z
                        );
                    }
                }
            );

            colorsPosition = 16f * new Vector2(xmin + width, ymin);
            lightMapTileArea = new Rectangle(xmin, ymin, width, height);
            lightMapRenderArea = new Rectangle(0, 0, height, width);

            FancyLightingModSystem.SmoothLightingBase = true;
        }

        internal void CalculateSmoothLighting(bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;

            if (!FancyLightingModSystem.SmoothLightingBase)
                GetBlurredLighting();

            if (!FancyLightingModSystem.SmoothLightingBase) return;

            int xmin = lightMapTileArea.X;
            int ymin = lightMapTileArea.Y;
            int width = lightMapTileArea.Width;
            int height = lightMapTileArea.Height;
            int ymax = ymin + height;

            if (finalLights is null || finalLights.Length < height * width)
            {
                finalLights = new Color[height * width];
            }

            if (background)
            {
                Parallel.For(
                    0,
                    width,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x1) =>
                    {
                        int i = height * x1;
                        int x = x1 + xmin;
                        for (int y = ymin; y < ymax; ++y)
                        {
                            // Also see: internal static bool IsGlowingTile(int x, int y)

                            // Illuminant Paint
                            if (Main.tile[x, y].WallColor == (byte)31)
                            {
                                finalLights[i++] = Color.White;
                                continue;
                            }

                            finalLights[i] = new Color(lightsPostBlur[i]);
                            ++i;
                        }
                    }
                );

                if (colorsBackground is null)
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, height, width, false, SurfaceFormat.Color);
                else if (colorsBackground.GraphicsDevice != Main.graphics.GraphicsDevice || (colorsBackground.Width < height || colorsBackground.Height < width))
                    colorsBackground = new Texture2D(Main.graphics.GraphicsDevice, Math.Max(colorsBackground.Width, height), Math.Max(colorsBackground.Height, width), false, SurfaceFormat.Color);

                colorsBackground.SetData(0, lightMapRenderArea, finalLights, 0, height * width);

                FancyLightingModSystem.SmoothLightingBackground = true;
            }
            else
            {
                Parallel.For(
                    0,
                    width,
                    new ParallelOptions { MaxDegreeOfParallelism = FancyLightingMod.ThreadCount },
                    (x1) =>
                    {
                        int i = height * x1;
                        int x = x1 + xmin;
                        for (int y = ymin; y < ymax; ++y)
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

                            finalLights[i] = new Color(lightsPostBlur[i]);

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

                FancyLightingModSystem.SmoothLightingForeground = true;
            }
        }

        internal void DrawSmoothLighting(RenderTarget2D target, bool background)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;
            if (!background && !FancyLightingModSystem.SmoothLightingForeground) return;
            if (background && !FancyLightingModSystem.SmoothLightingBackground) return;

            if (surface is null || surface.GraphicsDevice != Main.graphics.GraphicsDevice || (surface.Width != Main.instance.tileTarget.Width || surface.Height != Main.instance.tileTarget.Height))
            {
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }

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

            // Hacky workaround for getting multiply blend to work with alpha blending

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
