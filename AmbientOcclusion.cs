using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Capture;

namespace FancyLighting;

internal sealed class AmbientOcclusion
{
    private RenderTarget2D _drawTarget1;
    private RenderTarget2D _drawTarget2;

    private RenderTarget2D _cameraModeTarget1;
    private RenderTarget2D _cameraModeTarget2;
    private RenderTarget2D _cameraModeTarget3;

    private RenderTarget2D _tileEntityTarget;

    internal bool _drawingTileEntities;

    private Shader _alphaToGrayscaleShader;
    private Shader _alphaToLightGrayscaleShader;
    private Shader _blurShader;
    private Shader _finalBlurShader;

    public AmbientOcclusion()
    {
        _alphaToGrayscaleShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToGrayscale"
        );
        _alphaToLightGrayscaleShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToLighterGrayscale"
        );
        _blurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "Blur"
        );
        _finalBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "FinalBlur"
        );

        _drawingTileEntities = false;
    }

    public void Unload()
    {
        _drawTarget1?.Dispose();
        _drawTarget2?.Dispose();
        _cameraModeTarget1?.Dispose();
        _cameraModeTarget2?.Dispose();
        _cameraModeTarget3?.Dispose();
        _tileEntityTarget?.Dispose();
        EffectLoader.UnloadEffect(ref _alphaToGrayscaleShader);
        EffectLoader.UnloadEffect(ref _alphaToLightGrayscaleShader);
        EffectLoader.UnloadEffect(ref _blurShader);
        EffectLoader.UnloadEffect(ref _finalBlurShader);
    }

    private void InitSurfaces()
    {
        TextureMaker.MakeSize(ref _drawTarget1, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
        TextureMaker.MakeSize(ref _drawTarget2, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
    }

    internal RenderTarget2D ApplyAmbientOcclusion(bool doDraw = true)
    {
        if (!LightingConfig.Instance.AmbientOcclusionEnabled())
        {
            return null;
        }

        InitSurfaces();

        ApplyAmbientOcclusionInner(
            Main.instance.wallTarget,
            Main.instance.tileTarget,
            Main.instance.tile2Target,
            Main.sceneTilePos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            Main.sceneTile2Pos - (Main.screenPosition - new Vector2(Main.offScreenRange)),
            _drawTarget1,
            _drawTarget2,
            doDraw,
            out bool useTarget2
        );

        if (doDraw)
        {
            Main.instance.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin();
            Main.spriteBatch.Draw(
                useTarget2 ? _drawTarget1 : _drawTarget2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
        }

        Main.instance.GraphicsDevice.SetRenderTarget(null);

        return doDraw
            ? null
            : useTarget2 ? _drawTarget1 : _drawTarget2;
    }

    internal RenderTarget2D ApplyAmbientOcclusionCameraMode(
        RenderTarget2D screenTarget,
        RenderTarget2D wallTarget,
        CaptureBiome biome,
        bool doDraw = true
    )
    {
        TextureMaker.MakeSize(ref _cameraModeTarget1, screenTarget.Width, screenTarget.Height);
        TextureMaker.MakeSize(ref _cameraModeTarget2, screenTarget.Width, screenTarget.Height);
        TextureMaker.MakeSize(ref _cameraModeTarget3, screenTarget.Width, screenTarget.Height);

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

        bool extraLayer =
            LightingConfig.Instance.DoNonSolidAmbientOcclusion
            || LightingConfig.Instance.DoTileEntityAmbientOcclusion;

        if (extraLayer)
        {
            Main.instance.GraphicsDevice.SetRenderTarget(_cameraModeTarget2);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
        }
        if (LightingConfig.Instance.DoNonSolidAmbientOcclusion)
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

        ApplyAmbientOcclusionInner(
            wallTarget,
            _cameraModeTarget1,
            _cameraModeTarget2,
            Vector2.Zero,
            Vector2.Zero,
            _cameraModeTarget3,
            _cameraModeTarget2,
            doDraw,
            out bool useTarget2
        );

        if (doDraw)
        {
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
                useTarget2 ? _cameraModeTarget3 : _cameraModeTarget2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
        }

        return doDraw
            ? null
            : useTarget2 ? _cameraModeTarget3 : _cameraModeTarget2;
    }

    private void ApplyAmbientOcclusionInner(
        RenderTarget2D wallTarget,
        RenderTarget2D tileTarget,
        RenderTarget2D tile2Target,
        Vector2 tileTargetPosition,
        Vector2 tile2TargetPosition,
        RenderTarget2D target1,
        RenderTarget2D target2,
        bool doDraw,
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

            Shader shader = finalPass ? _finalBlurShader : _blurShader;
            shader
                .SetParameter("BlurSize", new Vector2(
                    (float)dx / surfaceSource.Width,
                    (float)dy / surfaceSource.Height))
                .SetParameter("BrightnessIncrease", raiseBrightness)
                .Apply();

            Main.spriteBatch.Draw(
                surfaceSource,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            useTarget2 = !useTarget2;
        }

        bool drawNonSolidTiles = LightingConfig.Instance.DoNonSolidAmbientOcclusion;
        bool drawTileEntities = LightingConfig.Instance.DoTileEntityAmbientOcclusion;

        if (!(drawNonSolidTiles || drawTileEntities))
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
            _alphaToGrayscaleShader.Apply();
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
                TextureMaker.MakeSize(
                    ref _tileEntityTarget,
                    Main.instance.tileTarget.Width,
                    Main.instance.tileTarget.Height
                );

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

            _alphaToGrayscaleShader.Apply();
            Main.spriteBatch.Draw(
                tileTarget,
                tileTargetPosition,
                Color.White
            );

            _alphaToLightGrayscaleShader.Apply();

            if (drawNonSolidTiles && tile2Target is not null)
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

        float alpha = LightingConfig.Instance.AmbientOcclusionAlpha();
        if (LightingConfig.Instance.UseGammaCorrection())
        {
            alpha = MathF.Pow(alpha, 2.2f);
        }

        // We need to switch between render targets
        useTarget2 = true;
        switch (LightingConfig.Instance.AmbientOcclusionRadius)
        {
        case 1:
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        case 2:
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        case 3:
            ApplyBlurPass(ref useTarget2, 2, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 2, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        case 4:
            ApplyBlurPass(ref useTarget2, 3, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 3, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        case 5:
            ApplyBlurPass(ref useTarget2, 4, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 4, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        case 6:
        default:
            ApplyBlurPass(ref useTarget2, 5, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 5, false);
            ApplyBlurPass(ref useTarget2, 1, 0, false);
            ApplyBlurPass(ref useTarget2, 0, 1, true, alpha);
            break;
        }

        if (!doDraw)
        {
            return;
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
