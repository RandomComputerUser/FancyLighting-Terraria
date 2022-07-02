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

            GameShaders.Misc["FancyLighting:AOPrePass"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "AlphaToGrayscale"
                );

            GameShaders.Misc["FancyLighting:AOBlur"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Shaders/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "Blur"
                );

        }

        internal void Unload()
        {
            surface?.Dispose();
            surface2?.Dispose();
        }

        internal void initSurfaces()
        {
            if (surface is null 
                || surface.GraphicsDevice != Main.graphics.GraphicsDevice
                || surface.Width != Main.instance.tileTarget.Width
                || surface.Height != Main.instance.tileTarget.Height)
            {
                surface?.Dispose();
                surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
            if (surface2 is null
                || surface2.GraphicsDevice != Main.graphics.GraphicsDevice
                || surface2.Width != Main.instance.tileTarget.Width
                || surface2.Height != Main.instance.tileTarget.Height)
            {
                surface2?.Dispose();
                surface2 = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
        }

        internal void ApplyBlurPass(ref bool useSurface2, int dx, int dy, bool finalPass, float raiseBrightness=0f)
        {
            var surfaceDestination = useSurface2 ? surface2 : surface;
            var surfaceSource = useSurface2 ? surface : surface2;

            Main.instance.GraphicsDevice.SetRenderTarget(surfaceDestination);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            GameShaders.Misc["FancyLighting:AOBlur"]
                .UseShaderSpecificData(new Vector4((float)dx / surfaceSource.Width, (float)dy / surfaceSource.Height, finalPass ? 1f : -1f, raiseBrightness))
                .Apply(null);
            Main.spriteBatch.Draw(
                surfaceSource,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            useSurface2 = !useSurface2;
        }

        internal void ApplyAmbientOcclusion()
        {
            if (!FancyLightingMod.AmbientOcclusionEnabled) return;

            initSurfaces();

            Main.instance.GraphicsDevice.SetRenderTarget(surface2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.instance.GraphicsDevice.SetRenderTarget(surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None, 
                RasterizerState.CullNone
            );
            GameShaders.Misc["FancyLighting:AOPrePass"].Apply(null);
            Main.spriteBatch.Draw(
                Main.instance.tileTarget,
                Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                Color.White
            );
            Main.spriteBatch.End();

            // For some reason we need to alternate between render targets, or else this doesn't work
            bool useSurface2 = true;

            switch (FancyLightingMod.AmbientOcclusionRadius)
            {
                case 1:
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 2:
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 3:
                    ApplyBlurPass(ref useSurface2, 2, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 2, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 4:
                    ApplyBlurPass(ref useSurface2, 3, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 3, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 5:
                default:
                    ApplyBlurPass(ref useSurface2, 4, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 4, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 6:
                    ApplyBlurPass(ref useSurface2, 5, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 5, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
                case 7:
                    ApplyBlurPass(ref useSurface2, 6, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 6, false);
                    ApplyBlurPass(ref useSurface2, 1, 0, false);
                    ApplyBlurPass(ref useSurface2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
                    break;
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
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
                useSurface2 ? surface : surface2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();


            Main.instance.GraphicsDevice.SetRenderTarget(null);

        }

    }
}
