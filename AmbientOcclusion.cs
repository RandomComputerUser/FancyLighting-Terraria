using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics.Shaders;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FancyLighting
{
    class AmbientOcclusion
    {

        internal RenderTarget2D surface;
        internal RenderTarget2D surface2;

        public AmbientOcclusion() {

            GameShaders.Misc["FancyLighting:PreAO"] =
                    new MiscShaderData(
                        new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/PreAO", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                        "AlphaToGrayscale"
                    );

            GameShaders.Misc["FancyLighting:AOHorizontalBlur"] =
                    new MiscShaderData(
                        new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/AOHorizontalBlur", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                        "HorizontalBlur"
                    );

            GameShaders.Misc["FancyLighting:AOVerticalBlur"] =
                    new MiscShaderData(
                        new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/AOVerticalBlur", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                        "VerticalBlur"
                    );

        }

        internal void initSurfaces()
        {
            if (surface is null || surface.GraphicsDevice != Main.graphics.GraphicsDevice || (surface.Width != Main.instance.tileTarget.Width || surface.Height != Main.instance.tileTarget.Height))
            {
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
            if (surface2 is null || surface2.GraphicsDevice != Main.graphics.GraphicsDevice || (surface2.Width != Main.instance.tileTarget.Width || surface2.Height != Main.instance.tileTarget.Height))
            {
                surface2 = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
        }

        internal void ApplyAmbientOcclusion()
        {
            if (!FancyLightingMod.AmbientOcclusionEnabled) return;

            initSurfaces();

            Main.instance.GraphicsDevice.SetRenderTarget(surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            GameShaders.Misc["FancyLighting:PreAO"].Apply(null);
            Main.spriteBatch.Draw(
                Main.instance.tileTarget,
                Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                Color.White
            );
            Main.spriteBatch.End();

            // For some reason we need to switch between render targets, or else this doesn't work
            Main.instance.GraphicsDevice.SetRenderTarget(surface2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            GameShaders.Misc["FancyLighting:AOHorizontalBlur"].UseShaderSpecificData(new Vector4(1f / surface.Width, 1f / surface.Height, 0f, 0f)).Apply(null);
            Main.spriteBatch.Draw(
                surface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(surface);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            GameShaders.Misc["FancyLighting:AOHorizontalBlur"].UseShaderSpecificData(new Vector4(1f / surface.Width, 1f / surface.Height, 0f, 0f)).Apply(null);
            Main.spriteBatch.Draw(
                surface2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(surface2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            GameShaders.Misc["FancyLighting:AOVerticalBlur"].UseShaderSpecificData(new Vector4(1f / surface.Width, 1f / surface.Height, 0f, 0f)).Apply(null);
            Main.spriteBatch.Draw(
                surface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            Main.instance.GraphicsDevice.SetRenderTarget(surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            GameShaders.Misc["FancyLighting:AOVerticalBlur"].UseShaderSpecificData(new Vector4(1f / surface.Width, 1f / surface.Height, 1f, 0f)).Apply(null);
            Main.spriteBatch.Draw(
                surface2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();


            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend
            );
            Main.spriteBatch.Draw(
                Main.instance.wallTarget,
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

            Main.instance.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
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
