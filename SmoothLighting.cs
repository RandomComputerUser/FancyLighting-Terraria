using Terraria;
using Terraria.Graphics.Light;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;

namespace FancyLighting
{
    class SmoothLighting
    {

        internal Texture2D colors;
        internal Vector2 colorsPosition;
        internal RenderTarget2D surface;
        internal Color[] lights;

        internal TileLightScanner TileLightScannerObj;

        public SmoothLighting() {
            TileLightScannerObj = new TileLightScanner();
        }

        internal static bool IsGlowingTile(int x, int y, bool inWallsMode)
        {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) return false;

            // Illuminant Paint
            if ((!inWallsMode && Main.tile[x, y].TileColor == (byte)31) || (inWallsMode && Main.tile[x, y].WallColor == (byte)31))
                return true;

            if (inWallsMode)
                return false;

            // Dangersense Potion
            if (Main.LocalPlayer.dangerSense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                return true;

            // Spelunker Potion
            if (Main.LocalPlayer.findTreasure && Main.IsTileSpelunkable(x, y))
                return true;

            // Crystal Shards and Gelatin Crystal, Meteorite Brick
            if (Main.tile[x, y].TileType == 129 || Main.tile[x, y].TileType == 370)
                return true;

            return false;
        }

        internal void CalculateSmoothLighting(bool walls)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;

            FancyLightingMod._overrideLightingColor = false;

            Vector4 screen = GetScreenCoords();

            int xmin = (int)(screen.X / 16.0) - 12;
            int xmax = (int)(screen.Z / 16.0) + 12;
            int ymin = (int)(screen.Y / 16.0) - 12;
            int ymax = (int)(screen.W / 16.0) + 12;

            // Reduce memory pressure and increase performance
            xmax = Math.Max(xmax, xmin + (int)(Math.Ceiling(Main.screenTarget.Width / (16.0 * Math.Abs(Main.GameZoomTarget))) + 0.5) + 24);
            ymax = Math.Max(ymax, ymin + (int)(Math.Ceiling(Main.screenTarget.Height / (16.0 * Math.Abs(Main.GameZoomTarget))) + 0.5) + 24);

            xmin = Math.Clamp(xmin, 0, Main.maxTilesX - 1);
            xmax = Math.Clamp(xmax, 0, Main.maxTilesX - 1);
            ymin = Math.Clamp(ymin, 0, Main.maxTilesY - 1);
            ymax = Math.Clamp(ymax, 0, Main.maxTilesY - 1);

            int width = (xmax - xmin) + 1;
            int height = (ymax - ymin) + 1;

            if (colors is null || colors.GraphicsDevice != Main.graphics.GraphicsDevice || (colors.Width != width || colors.Height != height))
            {
                colors = new Texture2D(Main.graphics.GraphicsDevice, width, height, false, SurfaceFormat.Color);
            }

            if (lights is null || lights.Length < width * height)
            {
                lights = new Color[width * height];
            }

            int i = 0;
            for (int y = ymin; y <= ymax; ++y)
            {
                for (int x = xmin; x <= xmax; ++x)
                {
                    lights[i++] = Lighting.GetColor(x, y);
                }
            }

            for (int y = ymin + 1; y <= ymax - 1; ++y)
            {
                for (int x = xmin + 1; x <= xmax - 1; ++x)
                {
                    i = width * (y - ymin) + (x - xmin);

                    int r = 1 * lights[i - width - 1].R + 2 * lights[i - width].R + 1 * lights[i - width + 1].R
                            + 2 * lights[i - 1].R         + 4 * lights[i].R         + 2 * lights[i + 1].R
                            + 1 * lights[i + width - 1].R + 2 * lights[i + width].R + 1 * lights[i + width + 1].R;

                    int g = 1 * lights[i - width - 1].G + 2 * lights[i - width].G + 1 * lights[i - width + 1].G
                            + 2 * lights[i - 1].G         + 4 * lights[i].G         + 2 * lights[i + 1].G
                            + 1 * lights[i + width - 1].G + 2 * lights[i + width].G + 1 * lights[i + width + 1].G;

                    int b = 1 * lights[i - width - 1].B + 2 * lights[i - width].B + 1 * lights[i - width + 1].B
                            + 2 * lights[i - 1].B         + 4 * lights[i].B         + 2 * lights[i + 1].B
                            + 1 * lights[i + width - 1].B + 2 * lights[i + width].B + 1 * lights[i + width + 1].B;

                    r = Math.Clamp(r / 16, 0, 255);
                    g = Math.Clamp(g / 16, 0, 255);
                    b = Math.Clamp(b / 16, 0, 255);

                    lights[i] = new Color((byte)r, (byte)g, (byte)b);
                }
            }

            i = 0;
            for (int y = ymin; y <= ymax; ++y)
            {
                for (int x = xmin; x <= xmax; ++x)
                {
                    var color = lights[i];

                    // Also see: internal static bool IsGlowingTile(int x, int y)

                    // Dangersense Potion
                    if (Main.LocalPlayer.dangerSense && Terraria.GameContent.Drawing.TileDrawing.IsTileDangerous(x, y, Main.LocalPlayer))
                    {
                        color.R = Math.Max((byte)255, color.R);
                        color.G = Math.Max((byte)50, color.G);
                        color.B = Math.Max((byte)50, color.B);
                    }

                    // Spelunker Potion
                    if (Main.LocalPlayer.findTreasure && Main.IsTileSpelunkable(x, y))
                    {
                        color.R = Math.Max((byte)200, color.R);
                        color.G = Math.Max((byte)170, color.G);
                    }

                    // Illuminant Paint
                    if ((!walls && Main.tile[x, y].TileColor == (byte)31) || (walls && Main.tile[x, y].WallColor == (byte)31))
                    {
                        color = Color.White;
                    }

                    // Crystal Shards and Gelatin Crystal
                    else if (Main.tile[x, y].TileType == 129)
                    {
                        color = Color.White;
                    }

                    // Meteorite Brick
                    else if (Main.tile[x, y].TileType == 370)
                    {
                        color.R = Math.Max((byte)219, color.R);
                        color.G = Math.Max((byte)104, color.G);
                        color.B = Math.Max((byte)19, color.B);
                    }

                    lights[i++] = color;
                }
            }

            colors.SetData(lights, 0, width * height);

            colorsPosition = 16f * new Vector2(xmin, ymin) - Main.screenPosition;
        }

        internal void initSurfaceAndColor()
        {
            if (surface is null || surface.GraphicsDevice != Main.graphics.GraphicsDevice || (surface.Width != Main.instance.tileTarget.Width || surface.Height != Main.instance.tileTarget.Height))
            {
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }

            if (colors is null)
            {
                Vector4 screen = GetScreenCoords();

                int xmin = (int)(screen.X / 16.0) - 1;
                int xmax = (int)(screen.Z / 16.0) + 1;
                int ymin = (int)(screen.Y / 16.0) - 1;
                int ymax = (int)(screen.W / 16.0) + 1;

                xmax = Math.Max(xmax, xmin + (int)(Math.Ceiling(Main.screenTarget.Width / (16.0 * Math.Abs(Main.GameZoomTarget))) + 0.5) + 2);
                ymax = Math.Max(ymax, ymin + (int)(Math.Ceiling(Main.screenTarget.Height / (16.0 * Math.Abs(Main.GameZoomTarget))) + 0.5) + 2);

                int width = (xmax - xmin) + 1;
                int height = (ymax - ymin) + 1;

                colors = new Texture2D(Main.graphics.GraphicsDevice, width, height, false, SurfaceFormat.Color);
                Main.graphics.GraphicsDevice.Clear(Color.Black);
            }
        }

        internal void DrawSmoothLighting(RenderTarget2D target)
        {
            if (!FancyLightingMod.SmoothLightingEnabled) return;

            initSurfaceAndColor();

            Main.instance.GraphicsDevice.SetRenderTarget(surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin();

            Main.spriteBatch.Draw(
                colors,
                colorsPosition + (Main.instance.tileTarget.Size() - Main.screenTarget.Size()) / 2f,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                16f,
                SpriteEffects.None,
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

        internal Vector4 GetScreenCoords()
        {
            Vector2 midScreen = Main.screenTarget.Size() / 2f;

            return new Vector4(
                Main.screenPosition.X + midScreen.X - midScreen.X / Math.Abs(Main.GameViewMatrix.Zoom.X),
                Main.screenPosition.Y + midScreen.Y - midScreen.Y / Math.Abs(Main.GameViewMatrix.Zoom.Y),
                Main.screenPosition.X + midScreen.X + midScreen.X / Math.Abs(Main.GameViewMatrix.Zoom.X),
                Main.screenPosition.Y + midScreen.Y + midScreen.Y / Math.Abs(Main.GameViewMatrix.Zoom.Y)
            );
        }

    }
}
