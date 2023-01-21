using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace FancyLighting;

internal sealed class AmbientOcclusion
{
    internal RenderTarget2D _drawTarget1;
    internal RenderTarget2D _drawTarget2;

    internal RenderTarget2D _cameraModeTarget1;
    internal RenderTarget2D _cameraModeTarget2;
    internal RenderTarget2D _cameraModeTarget3;

    internal RenderTarget2D _tileEntityTarget;

    internal bool _drawingTileEntities;

    public AmbientOcclusion()
    {
        GameShaders.Misc["FancyLighting:AOPrePass"] =
            new MiscShaderData(
                new Ref<Effect>(ModContent.Request<Effect>(
                    "FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad
                ).Value),
                "AlphaToGrayscale"
            );

        GameShaders.Misc["FancyLighting:AOPrePassLight"] =
            new MiscShaderData(
                new Ref<Effect>(ModContent.Request<Effect>(
                    "FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad
                ).Value),
                "AlphaToGrayscaleLighter"
            );

        GameShaders.Misc["FancyLighting:AOBlur"] =
            new MiscShaderData(
                new Ref<Effect>(ModContent.Request<Effect>(
                    "FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad
                ).Value),
                "Blur"
            );

        GameShaders.Misc["FancyLighting:AOFinalBlur"] =
            new MiscShaderData(
                new Ref<Effect>(ModContent.Request<Effect>(
                    "FancyLighting/Effects/AmbientOcclusion", ReLogic.Content.AssetRequestMode.ImmediateLoad
                ).Value),
                "BlurFinal"
            );

        _drawingTileEntities = false;
    }

    internal void Unload()
    {
        _drawTarget1?.Dispose();
        _drawTarget2?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _cameraModeTarget3?.Dispose();
        _tileEntityTarget?.Dispose();
        EffectLoader.UnloadEffect("FancyLighting:AOPrePass");
        EffectLoader.UnloadEffect("FancyLighting:AOPrePassLight");
        EffectLoader.UnloadEffect("FancyLighting:AOBlur");
        EffectLoader.UnloadEffect("FancyLighting:AOFinalBlur");
    }

    internal void initSurfaces()
    {
        TextureSize.MakeSize(ref _drawTarget1, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
        TextureSize.MakeSize(ref _drawTarget2, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
    }

    internal void ApplyAmbientOcclusion()
    {
        if (!FancyLightingMod.AmbientOcclusionEnabled)
        {
            return;
        }

        initSurfaces();

        ApplyAmbientOcclusionInner(
            Main.instance.wallTarget,
            Main.instance.tileTarget,
            FancyLightingMod.DoNonSolidAmbientOcclusion ? Main.instance.tile2Target : null,
            Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            Main.sceneTile2Pos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            _drawTarget1,
            _drawTarget2,
            out bool useSurface2
        ); ;

        Main.instance.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(
            useSurface2 ? _drawTarget1 : _drawTarget2,
            Vector2.Zero,
            Color.White
        );
        Main.spriteBatch.End();

        Main.instance.GraphicsDevice.SetRenderTarget(null);
    }

    internal void ApplyAmbientOcclusionCameraMode(
        RenderTarget2D screenTarget,
        RenderTarget2D wallTarget,
        CaptureBiome biome
    )
    {
        TextureSize.MakeSize(ref _cameraModeTarget1, screenTarget.Width, screenTarget.Height);
        TextureSize.MakeSize(ref _cameraModeTarget2, screenTarget.Width, screenTarget.Height);
        TextureSize.MakeSize(ref _cameraModeTarget3, screenTarget.Width, screenTarget.Height);

        Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.instance.TilesRenderer.PreDrawTiles(true, false, false);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        if (biome is null)
        {
            Main.instance.TilesRenderer.Draw(true, false, false);
        }
        else
        {
            Main.instance.TilesRenderer.Draw(true, false, false, Main.bloodMoon ? 9 : biome.WaterStyle);
        }

        Main.tileBatch.End();
        Main.spriteBatch.End();

        bool extraLayer = FancyLightingMod.DoNonSolidAmbientOcclusion || FancyLightingMod.DoTileEntityAmbientOcclusion;
        if (extraLayer)
        {
            Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeTarget2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
        }
        if (FancyLightingMod.DoNonSolidAmbientOcclusion)
        {
            Main.instance.TilesRenderer.PreDrawTiles(false, false, false);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            if (biome is null)
            {
                Main.instance.TilesRenderer.Draw(false, false, false);
            }
            else
            {
                Main.instance.TilesRenderer.Draw(false, false, false, Main.bloodMoon ? 9 : biome.WaterStyle);
            }

            Main.tileBatch.End();
            Main.spriteBatch.End();
        }
        if (FancyLightingMod.DoTileEntityAmbientOcclusion)
        {
            _drawingTileEntities = true;
            try
            {
                Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
            }
            finally
            {
                _drawingTileEntities = false;
            }
        }

        ApplyAmbientOcclusionInner(
            wallTarget,
            _cameraModeTarget1,
            extraLayer ? _cameraModeTarget2 : null,
            Vector2.Zero,
            Vector2.Zero,
            _cameraModeTarget3,
            _cameraModeTarget2,
            out bool useSurface2
        );

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
            useSurface2 ? _cameraModeTarget3 : _cameraModeTarget2,
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
        RenderTarget2D target1,
        RenderTarget2D target2,
        out bool useTarget2
    )
    {
        void ApplyBlurPass(ref bool useTarget2, int dx, int dy, bool finalPass, float raiseBrightness = 0f)
        {
            RenderTarget2D surfaceDestination = useTarget2 ? target2 : target1;
            RenderTarget2D surfaceSource = useTarget2 ? target1 : target2;

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
                    .UseShaderSpecificData(new Vector4(
                        (float)dx / surfaceSource.Width,
                        (float)dy / surfaceSource.Height,
                        raiseBrightness,
                        0f))
                    .Apply(null);
            }
            else
            {
                GameShaders.Misc["FancyLighting:AOBlur"]
                    .UseShaderSpecificData(new Vector4(
                        (float)dx / surfaceSource.Width,
                        (float)dy / surfaceSource.Height,
                        0f,
                        0f))
                    .Apply(null);
            }
            Main.spriteBatch.Draw(
                surfaceSource,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            useTarget2 = !useTarget2;
        }

        bool drawTileEntities = FancyLightingMod.DoTileEntityAmbientOcclusion;
        if (tile2Target is null && !drawTileEntities)
        {
            Main.instance.GraphicsDevice.SetRenderTarget(target1);
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
                    if (_tileEntityTarget is null
                    || _tileEntityTarget.GraphicsDevice != Main.graphics.GraphicsDevice
                    || _tileEntityTarget.Width != Main.instance.tileTarget.Width
                    || _tileEntityTarget.Height != Main.instance.tileTarget.Height)
                    {
                        _tileEntityTarget?.Dispose();
                        _tileEntityTarget = new RenderTarget2D(
                            Main.graphics.GraphicsDevice,
                            Main.instance.tileTarget.Width,
                            Main.instance.tileTarget.Height
                        );
                    }

                    Main.instance.GraphicsDevice.SetRenderTarget(_tileEntityTarget);
                    Main.instance.GraphicsDevice.Clear(Color.Transparent);
                    Vector2 currentZoom = Main.GameViewMatrix.Zoom;
                    Main.GameViewMatrix.Zoom = Vector2.One;

                    _drawingTileEntities = true;
                    try
                    {
                        Main.instance.TilesRenderer.PostDrawTiles(false, false, false);
                        Main.instance.TilesRenderer.PostDrawTiles(true, false, false);
                    }
                    finally
                    {
                        _drawingTileEntities = false;
                    }

                    Main.GameViewMatrix.Zoom = currentZoom;
                }
            }

            Main.instance.GraphicsDevice.SetRenderTarget(target1);
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
                    _tileEntityTarget,
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
        useTarget2 = true;
        switch (FancyLightingMod.AmbientOcclusionRadius)
        {
        case 1:
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
            break;
        case 2:
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
            break;
        case 3:
            ApplyBlurPass(ref useTarget2, 2, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 2, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
            break;
        case 4:
        default:
            ApplyBlurPass(ref useTarget2, 3, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 3, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
            break;
        case 5:
            ApplyBlurPass(ref useTarget2, 4, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 4, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
            break;
        case 6:
            ApplyBlurPass(ref useTarget2, 5, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 5, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, FancyLightingMod.AmbientOcclusionIntensity);
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
