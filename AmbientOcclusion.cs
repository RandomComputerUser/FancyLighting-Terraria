using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.ModLoader;
using Terraria.Graphics.Shaders;

namespace FancyLighting
{
    internal sealed class AmbientOcclusion
    {
        internal RenderTarget2D _surface;
        internal RenderTarget2D _surface2;

        public AmbientOcclusion() {

            GameShaders.Misc["FancyLighting:AOPrePass"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "AlphaToGrayscale"
                );

            GameShaders.Misc["FancyLighting:AOBlur"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "Blur"
                );

            GameShaders.Misc["FancyLighting:AOFinalBlur"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "BlurFinal"
                );

        }

        internal void Unload()
        {
            _surface?.Dispose();
            _surface2?.Dispose();
            GameShaders.Misc["FancyLighting:AOPrePass"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:AOPrePass");
            GameShaders.Misc["FancyLighting:AOBlur"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:AOBlur");
            GameShaders.Misc["FancyLighting:AOFinalBlur"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:AOFinalBlur");
        }

        internal void initSurfaces()
        {
            if (_surface is null 
                || _surface.GraphicsDevice != Main.graphics.GraphicsDevice
                || _surface.Width != Main.instance.tileTarget.Width
                || _surface.Height != Main.instance.tileTarget.Height)
            {
                _surface?.Dispose();
                _surface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
            if (_surface2 is null
                || _surface2.GraphicsDevice != Main.graphics.GraphicsDevice
                || _surface2.Width != Main.instance.tileTarget.Width
                || _surface2.Height != Main.instance.tileTarget.Height)
            {
                _surface2?.Dispose();
                _surface2 = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
            }
        }

        internal void ApplyBlurPass(ref bool useSurface2, int dx, int dy, bool finalPass, float raiseBrightness=0f)
        {
            var surfaceDestination = useSurface2 ? _surface2 : _surface;
            var surfaceSource = useSurface2 ? _surface : _surface2;

            Main.instance.GraphicsDevice.SetRenderTarget(surfaceDestination);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            if (finalPass)
            {
                GameShaders.Misc["FancyLighting:AOFinalBlur"]
                    .UseShaderSpecificData(new Vector4((float)dx / surfaceSource.Width, (float)dy / surfaceSource.Height, raiseBrightness, 0f))
                    .Apply(null);
            }
            else
            {
                GameShaders.Misc["FancyLighting:AOBlur"]
                    .UseShaderSpecificData(new Vector4((float)dx / surfaceSource.Width, (float)dy / surfaceSource.Height, 0f, 0f))
                    .Apply(null);
            }
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

            Main.instance.GraphicsDevice.SetRenderTarget(_surface2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.instance.GraphicsDevice.SetRenderTarget(_surface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
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

            // We need to switch between render targets
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
                useSurface2 ? _surface : _surface2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();


            Main.instance.GraphicsDevice.SetRenderTarget(null);

        }
    }
}
