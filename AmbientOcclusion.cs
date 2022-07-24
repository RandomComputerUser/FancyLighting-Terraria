using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

using System;

namespace FancyLighting
{
    internal sealed class AmbientOcclusion
    {
        internal RenderTarget2D _surface;
        internal RenderTarget2D _surface2;

        internal RenderTarget2D _cameraModeSurface;
        internal RenderTarget2D _cameraModeSurface2;
        internal RenderTarget2D _cameraModeSurface3;

        internal RenderTarget2D _tileEntitySurface;

        public AmbientOcclusion() {

            GameShaders.Misc["FancyLighting:AOPrePass"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "AlphaToGrayscale"
                );

            GameShaders.Misc["FancyLighting:AOPrePassLight"] =
                new MiscShaderData(
                    new Ref<Effect>(ModContent.Request<Effect>("FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value),
                    "AlphaToGrayscaleLighter"
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
            _cameraModeSurface?.Dispose();
            _cameraModeSurface2?.Dispose();
            _cameraModeSurface3?.Dispose();
            _tileEntitySurface?.Dispose();
            GameShaders.Misc["FancyLighting:AOPrePass"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:AOPrePass");
            GameShaders.Misc["FancyLighting:AOPrePassLight"]?.Shader?.Dispose();
            GameShaders.Misc.Remove("FancyLighting:AOPrePassLight");
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

        internal void ApplyAmbientOcclusion()
        {
            if (!FancyLightingMod.AmbientOcclusionEnabled) return;

            initSurfaces();

            ApplyAmbientOcclusionInner(
                Main.instance.wallTarget,
                Main.instance.tileTarget,
                FancyLightingMod.DoNonSolidAmbientOcclusion ? Main.instance.tile2Target : null,
                Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                Main.sceneTile2Pos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
                _surface, 
                _surface2,
                out bool useSurface2
            );;

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

        internal void ApplyAmbientOcclusionCameraMode(RenderTarget2D screenTarget, RenderTarget2D wallTarget, CaptureBiome biome)
        {
            if (_cameraModeSurface is null
                || _cameraModeSurface.GraphicsDevice != Main.graphics.GraphicsDevice
                || _cameraModeSurface.Width != screenTarget.Width
                || _cameraModeSurface.Height != screenTarget.Height)
            {
                _cameraModeSurface?.Dispose();
                _cameraModeSurface = new RenderTarget2D(Main.graphics.GraphicsDevice, screenTarget.Width, screenTarget.Height);
            }

            if (_cameraModeSurface2 is null
                || _cameraModeSurface2.GraphicsDevice != Main.graphics.GraphicsDevice
                || _cameraModeSurface2.Width != screenTarget.Width
                || _cameraModeSurface2.Height != screenTarget.Height)
            {
                _cameraModeSurface2?.Dispose();
                _cameraModeSurface2 = new RenderTarget2D(Main.graphics.GraphicsDevice, screenTarget.Width, screenTarget.Height);
            }

            if (_cameraModeSurface3 is null
                || _cameraModeSurface3.GraphicsDevice != Main.graphics.GraphicsDevice
                || _cameraModeSurface3.Width != screenTarget.Width
                || _cameraModeSurface3.Height != screenTarget.Height)
            {
                _cameraModeSurface3?.Dispose();
                _cameraModeSurface3 = new RenderTarget2D(Main.graphics.GraphicsDevice, screenTarget.Width, screenTarget.Height);
            }

            Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeSurface);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.instance.TilesRenderer.PreDrawTiles(true, false, false);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            if (biome is null)
                Main.instance.TilesRenderer.Draw(true, false, false);
            else
                Main.instance.TilesRenderer.Draw(true, false, false, Main.bloodMoon ? 9 : biome.WaterStyle);
            Main.tileBatch.End();
            Main.spriteBatch.End();

            bool extraLayer = FancyLightingMod.DoNonSolidAmbientOcclusion || FancyLightingMod.DoTileEntityAmbientOcclusion;
            if (extraLayer)
            {
                Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeSurface2);
                Main.instance.GraphicsDevice.Clear(Color.Transparent);
            }
            if (FancyLightingMod.DoNonSolidAmbientOcclusion)
            {
                Main.instance.TilesRenderer.PreDrawTiles(false, false, false);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                if (biome is null)
                    Main.instance.TilesRenderer.Draw(false, false, false);
                else
                    Main.instance.TilesRenderer.Draw(false, false, false, Main.bloodMoon ? 9 : biome.WaterStyle);
                Main.tileBatch.End();
                Main.spriteBatch.End();
            }
            if (FancyLightingMod.DoTileEntityAmbientOcclusion)
            {
                Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
            }

            ApplyAmbientOcclusionInner(
                wallTarget,
                _cameraModeSurface,
                extraLayer ? _cameraModeSurface2 : null,
                Vector2.Zero,
                Vector2.Zero,
                _cameraModeSurface3,
                _cameraModeSurface2,
                out bool useSurface2
            );

            Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeSurface);
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
                _cameraModeSurface,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.Draw(
                useSurface2 ? _cameraModeSurface3 : _cameraModeSurface2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
        }

        private void ApplyAmbientOcclusionInner(
            RenderTarget2D wallTarget,
            RenderTarget2D tileTarget,
            RenderTarget2D tile2Target,
            Vector2 tileTargetPosition,
            Vector2 tile2TargetPosition,
            RenderTarget2D surface1,
            RenderTarget2D surface2,
            out bool useSurface2)
        {
            void ApplyBlurPass(ref bool useSurface2, int dx, int dy, bool finalPass, float raiseBrightness = 0f)
            {
                var surfaceDestination = useSurface2 ? surface2 : surface1;
                var surfaceSource = useSurface2 ? surface1 : surface2;

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

            bool drawTileEntities = FancyLightingMod.DoTileEntityAmbientOcclusion;
            if (tile2Target is null && !drawTileEntities)
            {
                Main.instance.GraphicsDevice.SetRenderTarget(surface1);
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
                    tileTarget,
                    tileTargetPosition,
                    Color.White
                );
                Main.spriteBatch.End();
            }
            else
            {
                if (drawTileEntities)
                {
                    if (FancyLightingMod.DoTileEntityAmbientOcclusion)
                    {
                        if (_tileEntitySurface is null
                        || _tileEntitySurface.GraphicsDevice != Main.graphics.GraphicsDevice
                        || _tileEntitySurface.Width != Main.instance.tileTarget.Width
                        || _tileEntitySurface.Height != Main.instance.tileTarget.Height)
                        {
                            _tileEntitySurface?.Dispose();
                            _tileEntitySurface = new RenderTarget2D(Main.graphics.GraphicsDevice, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
                        }

                        Main.instance.GraphicsDevice.SetRenderTarget(_tileEntitySurface);
                        Main.instance.GraphicsDevice.Clear(Color.Transparent);
                        Vector2 currentZoom = Main.GameViewMatrix.Zoom;
                        Main.GameViewMatrix.Zoom = Vector2.One;
                        Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                        Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
                        Main.GameViewMatrix.Zoom = currentZoom;
                    }
                }

                Main.instance.GraphicsDevice.SetRenderTarget(surface1);
                Main.instance.GraphicsDevice.Clear(Color.White);

                Main.spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    FancyLightingMod.MultiplyBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );
                GameShaders.Misc["FancyLighting:AOPrePass"].Apply(null);
                Main.spriteBatch.Draw(
                    tileTarget,
                    tileTargetPosition,
                    Color.White
                );
                GameShaders.Misc["FancyLighting:AOPrePassLight"].Apply(null);
                if (tile2Target is not null)
                {
                    Main.spriteBatch.Draw(
                        tile2Target,
                        tile2TargetPosition,
                        Color.White
                    );
                }
                if (drawTileEntities)
                {
                    Main.spriteBatch.Draw(
                        _tileEntitySurface,
                        new Vector2(Main.offScreenRange),
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        1f,
                        Main.GameViewMatrix.Effects,
                        1f
                    );
                }
                
                Main.spriteBatch.End();
            }

            // We need to switch between render targets
            useSurface2 = true;
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
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(
                wallTarget,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
        }
    }
}
