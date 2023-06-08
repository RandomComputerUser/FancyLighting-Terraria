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

    private Shader _alphaToMonochromeShader;
    private Shader _alphaToLightMonochromeShader;
    private Shader _semicircleBlurShader;
    private Shader _largeSemicircleBlurShader;
    private Shader _blurShader;
    private Shader _finalBlurShader;

    public AmbientOcclusion()
    {
        _alphaToMonochromeShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToMonochrome"
        );
        _alphaToLightMonochromeShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "AlphaToLightMonochrome"
        );
        _semicircleBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "SemicircleBlur"
        );
        _largeSemicircleBlurShader = EffectLoader.LoadEffect(
            "FancyLighting/Effects/AmbientOcclusion",
            "LargeSemicircleBlur"
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
        EffectLoader.UnloadEffect(ref _alphaToMonochromeShader);
        EffectLoader.UnloadEffect(ref _alphaToLightMonochromeShader);
        EffectLoader.UnloadEffect(ref _semicircleBlurShader);
        EffectLoader.UnloadEffect(ref _largeSemicircleBlurShader);
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
                SamplerState.LinearClamp,
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
                SamplerState.LinearClamp,
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
                SamplerState.LinearClamp,
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
            ref bool useTarget2, int dx, int dy, Shader shader, float blurPower = 0f
        )
        {
            RenderTarget2D surfaceDestination = useTarget2 ? target2 : target1;
            RenderTarget2D surfaceSource = useTarget2 ? target1 : target2;

            Main.graphics.GraphicsDevice.SetRenderTarget(surfaceDestination);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            shader
                .SetParameter("BlurSize", new Vector2(
                    (float)dx / surfaceSource.Width,
                    (float)dy / surfaceSource.Height))
                .SetParameter("BlurPower", blurPower)
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
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            _alphaToMonochromeShader.Apply();
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

            Main.graphics.GraphicsDevice.SetRenderTarget(target1);
            Main.graphics.GraphicsDevice.Clear(Color.White);

            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                FancyLightingMod.MultiplyBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );

            _alphaToMonochromeShader.Apply();
            Main.spriteBatch.Draw(
                tileTarget,
                tileTargetPosition,
                Color.White
            );

            _alphaToLightMonochromeShader.Apply();

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

        float power = LightingConfig.Instance.AmbientOcclusionPower();
        if (LightingConfig.Instance.DoGammaCorrection())
        {
            power *= 2.2f;
        }

        int radius = LightingConfig.Instance.AmbientOcclusionRadius;
        Shader firstShader = radius switch
        {
            1 => _semicircleBlurShader,
            2 => _semicircleBlurShader,
            3 => _semicircleBlurShader,
            4 => _semicircleBlurShader,
            5 => _largeSemicircleBlurShader,
            6 => _largeSemicircleBlurShader,
            _ => _semicircleBlurShader,
        };
        int firstShaderBlurStep = radius switch
        {
            1 => 1, // 7 * 1 = 7
            2 => 2, // 7 * 2 = 14
            3 => 3, // 7 * 3 = 21
            4 => 4, // 7 * 4 = 28
            5 => 3, // 11 * 3 = 33
            6 => 4, // 11 * 4 = 44
            _ => 3,
        };

        // We need to switch between render targets
        useTarget2 = true;
        ApplyBlurPass(ref useTarget2, firstShaderBlurStep, 0, firstShader);
        ApplyBlurPass(ref useTarget2, 0, firstShaderBlurStep, firstShader);
        ApplyBlurPass(ref useTarget2, 1, 0, _blurShader);
        ApplyBlurPass(ref useTarget2, 0, 1, _finalBlurShader, power);

        if (!doDraw)
        {
            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            FancyLightingMod.MultiplyBlend,
            SamplerState.LinearClamp,
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
