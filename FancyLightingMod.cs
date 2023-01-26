using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

public sealed class FancyLightingMod : Mod
{
    public static BlendState MultiplyBlend { get; private set; }

    internal static bool _overrideLightingColor;
    internal static bool _inCameraMode;

    private FancyLightingEngine _fancyLightingEngineInstance;
    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;

    internal FieldInfo field_activeEngine;
    private FieldInfo field_activeLightMap;
    internal FieldInfo field_workingProcessedArea;
    internal FieldInfo field_colors;
    internal FieldInfo field_mask;

    private static RenderTarget2D _cameraModeTarget;
    internal static Rectangle _cameraModeArea;
    private static CaptureBiome _cameraModeBiome;

    private static RenderTarget2D _screenTarget1;
    private static RenderTarget2D _screenTarget2;

    public bool OverrideLightingColor
    {
        get => _overrideLightingColor;
        internal set
        {
            if (value == _overrideLightingColor)
            {
                return;
            }

            if (!Lighting.UsingNewLighting)
            {
                return;
            }

            object activeEngine = field_activeEngine.GetValue(null);
            if (activeEngine is not LightingEngine lightingEngine)
            {
                return;
            }

            LightMap lightMapInstance = (LightMap)field_activeLightMap.GetValue(lightingEngine);

            if (value)
            {
                _smoothLightingInstance._tmpLights = (Vector3[])field_colors.GetValue(lightMapInstance);
            }

            field_colors.SetValue(
                lightMapInstance,
                value ? _smoothLightingInstance._whiteLights : _smoothLightingInstance._tmpLights
            );

            _overrideLightingColor = value;
        }
    }

    public override void Load()
    {
        if (Main.netMode == NetmodeID.Server)
        {
            return;
        }

        _overrideLightingColor = false;
        _inCameraMode = false;

        BlendState blend = new()
        {
            ColorBlendFunction = BlendFunction.Add,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceColor
        };
        MultiplyBlend = blend;

        _smoothLightingInstance = new SmoothLighting(this);
        _ambientOcclusionInstance = new AmbientOcclusion();
        _fancyLightingEngineInstance = new FancyLightingEngine();

        field_activeEngine
            = typeof(Lighting).GetField("_activeEngine", BindingFlags.NonPublic | BindingFlags.Static);
        field_activeLightMap
            = typeof(LightingEngine).GetField("_activeLightMap", BindingFlags.NonPublic | BindingFlags.Instance);
        field_workingProcessedArea
            = typeof(LightingEngine).GetField("_workingProcessedArea", BindingFlags.NonPublic | BindingFlags.Instance);
        field_colors
            = typeof(LightMap).GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance);
        field_mask
            = typeof(LightMap).GetField("_mask", BindingFlags.NonPublic | BindingFlags.Instance);

        AddHooks();
        SkyColors.AddSkyColorsHooks();
    }

    public override void Unload()
    {
        Main.QueueMainThreadAction(
            () =>
            {
                // Do not dispose _cameraModeTarget
                // _cameraModeTarget comes from the Main class, so we don't own it
                _screenTarget1?.Dispose();
                _screenTarget2?.Dispose();
                _cameraModeTarget = null;
                _smoothLightingInstance?.Unload();
                _ambientOcclusionInstance?.Unload();
            }
        );

        base.Unload();
    }

    private void AddHooks()
    {
        On.Terraria.Lighting.GetColor9Slice_int_int_refVector3Array += _GetColor9Slice_int_int_refVector3Array;
        On.Terraria.Lighting.GetColor4Slice_int_int_refVector3Array += _GetColor4Slice_int_int_refVector3Array;
        On.Terraria.GameContent.Drawing.TileDrawing.PostDrawTiles += _PostDrawTiles;
        On.Terraria.Main.RenderWater += _RenderWater;
        On.Terraria.Main.DrawWaters += _DrawWaters;
        On.Terraria.Main.RenderBackground += _RenderBackground;
        On.Terraria.Main.DrawBackground += _DrawBackground;
        On.Terraria.Main.RenderBlack += _RenderBlack;
        On.Terraria.Main.RenderTiles += _RenderTiles;
        On.Terraria.Main.RenderTiles2 += _RenderTiles2;
        On.Terraria.Main.RenderWalls += _RenderWalls;
        On.Terraria.Graphics.Light.LightingEngine.ProcessBlur += _ProcessBlur;
        On.Terraria.Graphics.Light.LightMap.Blur += _Blur;
        // For some reason the order in which these are added matters to ensure that camera mode works
        // Maybe DrawCapture needs to be added last
        On.Terraria.Main.DrawWalls += _DrawWalls;
        On.Terraria.Main.DrawTiles += _DrawTiles;
        On.Terraria.Main.DrawCapture += _DrawCapture;
    }

    private static void _GetColor9Slice_int_int_refVector3Array(
        On.Terraria.Lighting.orig_GetColor9Slice_int_int_refVector3Array orig,
        int x,
        int y,
        ref Vector3[] slices
    )
    {
        if (!_overrideLightingColor)
        {
            orig(x, y, ref slices);
            return;
        }

        for (int i = 0; i < 9; ++i)
        {
            slices[i] = Vector3.One;
        }
    }

    private static void _GetColor4Slice_int_int_refVector3Array(
        On.Terraria.Lighting.orig_GetColor4Slice_int_int_refVector3Array orig,
        int x,
        int y,
        ref Vector3[] slices
    )
    {
        if (!_overrideLightingColor)
        {
            orig(x, y, ref slices);
            return;
        }

        for (int i = 0; i < 4; ++i)
        {
            slices[i] = Vector3.One;
        }
    }

    // Tile entities
    private void _PostDrawTiles(
        On.Terraria.GameContent.Drawing.TileDrawing.orig_PostDrawTiles orig,
        Terraria.GameContent.Drawing.TileDrawing self,
        bool solidLayer,
        bool forRenderTargets,
        bool intoRenderTargets
    )
    {
        if (intoRenderTargets
            || !LightingConfig.Instance.DrawOverbright()
            || !LightingConfig.Instance.SmoothLightingEnabled()
            || LightingConfig.Instance.RenderOnlyLight
            || _ambientOcclusionInstance._drawingTileEntities)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        if ((Main.instance.GraphicsDevice.GetRenderTargets()?.Length ?? 0) < 1)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        RenderTarget2D target = (RenderTarget2D)Main.instance.GraphicsDevice.GetRenderTargets()[0].RenderTarget;

        TextureMaker.MakeSize(ref _screenTarget1, target.Width, target.Height);
        TextureMaker.MakeSize(ref _screenTarget2, target.Width, target.Height);

        Main.instance.GraphicsDevice.SetRenderTarget(_screenTarget1);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.instance.GraphicsDevice.SetRenderTarget(_screenTarget2);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        orig(self, solidLayer, forRenderTargets, intoRenderTargets);

        _smoothLightingInstance.CalculateSmoothLighting(false, _inCameraMode);
        _smoothLightingInstance.DrawSmoothLighting(_screenTarget2, false, true, target);

        Main.instance.GraphicsDevice.SetRenderTarget(target);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(_screenTarget1, Vector2.Zero, Color.White);
        Main.spriteBatch.Draw(_screenTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }

    private void _RenderWater(
        On.Terraria.Main.orig_RenderWater orig,
        Terraria.Main self
    )
    {
        if (LightingConfig.Instance.RenderOnlyLight && LightingConfig.Instance.SmoothLightingEnabled())
        {
            Main.instance.GraphicsDevice.SetRenderTarget(Main.waterTarget);
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.instance.GraphicsDevice.SetRenderTarget(null);
            return;
        }

        if (!LightingConfig.Instance.DrawOverbright() || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(false);
        orig(self);

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(Main.waterTarget, false, true);
    }

    private void _DrawWaters(
        On.Terraria.Main.orig_DrawWaters orig,
        Terraria.Main self,
        bool isBackground
    )
    {
        if (
            _inCameraMode
            || !LightingConfig.Instance.DrawOverbright()
            || !LightingConfig.Instance.SmoothLightingEnabled()
        )
        {
            orig(self, isBackground);
            return;
        }

        OverrideLightingColor = isBackground
            ? _smoothLightingInstance.DrawSmoothLightingBack
            : _smoothLightingInstance.DrawSmoothLightingFore;
        try
        {
            orig(self, isBackground);
        }
        finally
        {
            OverrideLightingColor = false;
        }
    }

    // Cave backgrounds
    private void _RenderBackground(
        On.Terraria.Main.orig_RenderBackground orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.ModifyBackgroundRendering())
        {
            orig(self);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(true);
        orig(self);

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(Main.instance.backgroundTarget, true, true);
        if (LightingConfig.Instance.DrawOverbright())
        {
            _smoothLightingInstance.DrawSmoothLighting(Main.instance.backWaterTarget, true, true);
        }
    }

    private void _DrawBackground(
        On.Terraria.Main.orig_DrawBackground orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.ModifyBackgroundRendering())
        {
            orig(self);
            return;
        }

        if (_inCameraMode)
        {
            _smoothLightingInstance.CalculateSmoothLighting(true, true);
            OverrideLightingColor = LightingConfig.Instance.SmoothLightingEnabled();

            Main.tileBatch.End();
            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.SetRenderTarget(
                _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
            );
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
            try
            {
                orig(self);
            }
            finally
            {
                OverrideLightingColor = false;
            }
            Main.tileBatch.End();
            Main.spriteBatch.End();

            _smoothLightingInstance.DrawSmoothLightingCameraMode(
                _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, true, false, true
            );

            Main.tileBatch.Begin();
            Main.spriteBatch.Begin();
        }
        else
        {
            OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
            try
            {
                orig(self);
            }
            finally
            {
                OverrideLightingColor = false;
            }
        }
    }

    private void _RenderBlack(
        On.Terraria.Main.orig_RenderBlack orig,
        Terraria.Main self
    )
    {
        bool initialLightingOverride = OverrideLightingColor;
        OverrideLightingColor = false;
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightingColor = initialLightingOverride;
        }
    }

    private void _RenderTiles(
        On.Terraria.Main.orig_RenderTiles orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(false);
        OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightingColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(Main.instance.tileTarget, false);
    }

    private void _RenderTiles2(
        On.Terraria.Main.orig_RenderTiles2 orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(false);
        OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightingColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(Main.instance.tile2Target, false);
    }

    private void _RenderWalls(
        On.Terraria.Main.orig_RenderWalls orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            if (LightingConfig.Instance.AmbientOcclusionEnabled())
            {
                _ambientOcclusionInstance.ApplyAmbientOcclusion();
            }
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(true);
        OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightingColor = false;
        }

        if (Main.drawToScreen)
        {
            return;
        }

        _smoothLightingInstance.DrawSmoothLighting(Main.instance.wallTarget, true);
        if (LightingConfig.Instance.AmbientOcclusionEnabled())
        {
            _ambientOcclusionInstance.ApplyAmbientOcclusion();
        }
    }

    private void _ProcessBlur(
        On.Terraria.Graphics.Light.LightingEngine.orig_ProcessBlur orig,
        Terraria.Graphics.Light.LightingEngine self
    )
    {
        if (!LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            orig(self);
            return;
        }

        _fancyLightingEngineInstance._lightMapArea = (Rectangle)field_workingProcessedArea.GetValue(self);
        orig(self);
    }

    private void _Blur(
        On.Terraria.Graphics.Light.LightMap.orig_Blur orig,
        Terraria.Graphics.Light.LightMap self
    )
    {
        if (
            !LightingConfig.Instance.SmoothLightingEnabled()
            && !LightingConfig.Instance.FancyLightingEngineEnabled()
        )
        {
            orig(self);
            return;
        }

        Vector3[] colors = (Vector3[])field_colors.GetValue(self);
        LightMaskMode[] lightDecay = (LightMaskMode[])field_mask.GetValue(self);
        if (colors is null || lightDecay is null)
        {
            orig(self);
            return;
        }

        if (LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            _fancyLightingEngineInstance.SpreadLight(self, colors, lightDecay, self.Width, self.Height);
        }
        else
        {
            orig(self);
        }

        if (LightingConfig.Instance.SmoothLightingEnabled())
        {
            _smoothLightingInstance.GetAndBlurLightMap(colors, self.Width, self.Height);
        }
    }

    private void _DrawWalls(
        On.Terraria.Main.orig_DrawWalls orig,
        Terraria.Main self
    )
    {
        if (!_inCameraMode)
        {
            orig(self);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(true, true);
        OverrideLightingColor = LightingConfig.Instance.SmoothLightingEnabled();

        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.instance.GraphicsDevice.SetRenderTarget(_smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget));
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        try
        {
            orig(self);
        }
        finally
        {
            OverrideLightingColor = false;
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget,
            _smoothLightingInstance._cameraModeTarget1,
            true,
            LightingConfig.Instance.AmbientOcclusionEnabled()
        );
        if (LightingConfig.Instance.AmbientOcclusionEnabled())
        {
            _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                _cameraModeTarget, _smoothLightingInstance._cameraModeTarget2, _cameraModeBiome
            );
        }

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _DrawTiles(
        On.Terraria.Main.orig_DrawTiles orig,
        Terraria.Main self,
        bool solidLayer,
        bool forRenderTargets,
        bool intoRenderTargets,
        int waterStyleOverride
    )
    {
        if (!_inCameraMode)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(false, true);
        OverrideLightingColor = LightingConfig.Instance.SmoothLightingEnabled();

        Main.tileBatch.End();
        Main.spriteBatch.End();
        Main.instance.GraphicsDevice.SetRenderTarget(_smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget));
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
        try
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
        }
        finally
        {
            OverrideLightingColor = false;
        }
        Main.tileBatch.End();
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, false, false
        );

        Main.tileBatch.Begin();
        Main.spriteBatch.Begin();
    }

    private void _DrawCapture(
        On.Terraria.Main.orig_DrawCapture orig,
        Terraria.Main self,
        Rectangle area,
        CaptureSettings settings
    )
    {
        if (LightingConfig.Instance.ModifyCameraModeRendering())
        {
            RenderTargetBinding[] renderTargets = Main.instance.GraphicsDevice.GetRenderTargets();
            _cameraModeTarget = renderTargets is null || renderTargets.Length < 1
                ? null
                : (RenderTarget2D)renderTargets[0].RenderTarget;
            _inCameraMode = _cameraModeTarget is not null;
        }
        else
        {
            _inCameraMode = false;
        }

        if (_inCameraMode)
        {
            _cameraModeArea = area;
            _cameraModeBiome = settings.Biome;
        }

        try
        {
            orig(self, area, settings);
        }
        finally
        {
            _inCameraMode = false;
        }
    }
}
