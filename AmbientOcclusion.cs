using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    private Shader _alphaToRedShader;
    private Shader _alphaToLightRedShader;
    private Shader _hemisphereBlurShader;
    private Shader _blurShader;
    private Shader _finalBlurShader;

    public AmbientOcclusion()
    {
        _alphaToRedShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToRed"
        );
        _alphaToLightRedShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToLightRed"
        );
        _hemisphereBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "HemisphereBlur"
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
        EffectLoader.UnloadEffect(ref _alphaToRedShader);
        EffectLoader.UnloadEffect(ref _alphaToLightRedShader);
        EffectLoader.UnloadEffect(ref _hemisphereBlurShader);
        EffectLoader.UnloadEffect(ref _blurShader);
        EffectLoader.UnloadEffect(ref _finalBlurShader);
    }

    private void InitSurfaces()
    {
        TextureUtil.MakeSize(ref _drawTarget1, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
        TextureUtil.MakeSize(ref _drawTarget2, Main.instance.tileTarget.Width, Main.instance.tileTarget.Height);
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
            Main.graphics.GraphicsDevice.SetRenderTarget(Main.instance.wallTarget);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(
                useTarget2 ? _drawTarget1 : _drawTarget2,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();
        }

        Main.graphics.GraphicsDevice.SetRenderTarget(null);

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
        TextureUtil.MakeSize(ref _cameraModeTarget1, screenTarget.Width, screenTarget.Height);
        TextureUtil.MakeSize(ref _cameraModeTarget2, screenTarget.Width, screenTarget.Height);
        TextureUtil.MakeSize(ref _cameraModeTarget3, screenTarget.Width, screenTarget.Height);

        Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
        Main.graphics.GraphicsDevice.Clear(Color.Transparent);
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
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget2);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
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
            Main.graphics.GraphicsDevice.SetRenderTarget(_cameraModeTarget1);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            Main.spriteBatch.Draw(
                screenTarget,
                Vector2.Zero,
                Color.White
            );
            Main.spriteBatch.End();

            Main.graphics.GraphicsDevice.SetRenderTarget(screenTarget);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
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
        void ApplyBlurPass(
            ref bool useTarget2, int dx, int dy, Shader shader, float blurPower = 0f, float blurMult = 0f
        )
        {
            RenderTarget2D surfaceDestination = useTarget2 ? target2 : target1;
            RenderTarget2D surfaceSource = useTarget2 ? target1 : target2;

            Main.graphics.GraphicsDevice.SetRenderTarget(surfaceDestination);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            shader
                .SetParameter("BlurSize", new Vector2(
                    (float)dx / surfaceSource.Width,
                    (float)dy / surfaceSource.Height))
                .SetParameter("BlurPower", blurPower)
                .SetParameter("BlurMult", blurMult)
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
            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            Main.graphics.GraphicsDevice.Clear(Color.Transparent);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _alphaToRedShader.Apply();
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
                TextureUtil.MakeSize(
                    ref _tileEntityTarget,
                    Main.instance.tileTarget.Width,
                    Main.instance.tileTarget.Height
                );

                Main.graphics.GraphicsDevice.SetRenderTarget(_tileEntityTarget);
                Main.graphics.GraphicsDevice.Clear(Color.Transparent);
                Vector2 currentZoom = Main.GameViewMatrix.Zoom;
                Vector2 currentScreenPosition = Main.screenPosition;
                Main.GameViewMatrix.Zoom = Vector2.One;
                Main.screenPosition -= new Vector2(Main.offScreenRange);

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
                    Main.screenPosition = currentScreenPosition;
                }
            }

            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            Main.graphics.GraphicsDevice.Clear(Color.White);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            _alphaToRedShader.Apply();
            Main.spriteBatch.Draw(
                tileTarget,
                tileTargetPosition,
                Color.White
            );

            _alphaToLightRedShader.Apply();

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
                    Vector2.Zero,
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

        float power = LightingConfig.Instance.AmbientOcclusionPower();
        if (LightingConfig.Instance.DoGammaCorrection())
        {
            power *= 2.2f;
        }
        float mult = LightingConfig.Instance.AmbientOcclusionMult();

        int radius = LightingConfig.Instance.AmbientOcclusionRadius;
        int firstShaderBlurStep = radius switch
        {
            2 => 1,
            3 => 2,
            4 => 3,
            5 => 4,
            _ => 2,
        };

        // We need to switch between render targets
        useTarget2 = true;
        ApplyBlurPass(ref useTarget2, firstShaderBlurStep, 0, _hemisphereBlurShader);
        ApplyBlurPass(ref useTarget2, 0, firstShaderBlurStep, _hemisphereBlurShader);
        ApplyBlurPass(ref useTarget2, 1, 0, _blurShader);
        ApplyBlurPass(ref useTarget2, 0, 1, _finalBlurShader, power, mult);

        if (!doDraw)
        {
            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
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
