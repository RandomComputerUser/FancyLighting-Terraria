using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.Graphics;
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

    private SmoothLighting _smoothLightingInstance;
    private AmbientOcclusion _ambientOcclusionInstance;
    private IFancyLightingEngine _fancyLightingEngineInstance;

    internal FieldInfo field_activeEngine;
    private FieldInfo field_activeLightMap;
    internal FieldInfo field_workingProcessedArea;
    internal FieldInfo field_colors;
    internal FieldInfo field_mask;

    private delegate void TileDrawingMethod(Terraria.GameContent.Drawing.TileDrawing self);

    private TileDrawingMethod method_DrawMultiTileVines;
    private TileDrawingMethod method_DrawMultiTileGrass;
    private TileDrawingMethod method_DrawVoidLenses;
    private TileDrawingMethod method_DrawTeleportationPylons;
    private TileDrawingMethod method_DrawMasterTrophies;
    private TileDrawingMethod method_DrawGrass;
    private TileDrawingMethod method_DrawAnyDirectionalGrass;
    private TileDrawingMethod method_DrawTrees;
    private TileDrawingMethod method_DrawVines;
    private TileDrawingMethod method_DrawReverseVines;

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

        method_DrawMultiTileVines
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawMultiTileVines", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawMultiTileGrass
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawMultiTileGrass", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawVoidLenses
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawVoidLenses", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawTeleportationPylons
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawTeleportationPylons", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawMasterTrophies
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawMasterTrophies", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawGrass
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawGrass", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawAnyDirectionalGrass
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawAnyDirectionalGrass", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawTrees
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawTrees", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawVines
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawVines", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );
        method_DrawReverseVines
            = (TileDrawingMethod)Delegate.CreateDelegate(
                typeof(TileDrawingMethod),
                typeof(TileDrawing).GetMethod(
                    "DrawReverseVines", BindingFlags.NonPublic | BindingFlags.Instance, new Type[] { }
                )
            );

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
        Terraria.On_Lighting.GetSubLight += _GetSubLight;
        Terraria.On_Lighting.GetCornerColors += _GetCornerColors;
        Terraria.On_Lighting.GetColor9Slice_int_int_refVector3Array += _GetColor9Slice_int_int_refVector3Array;
        Terraria.On_Lighting.GetColor4Slice_int_int_refVector3Array += _GetColor4Slice_int_int_refVector3Array;
        Terraria.On_Lighting.GetColor9Slice_int_int_refColorArray += _GetColor9Slice_int_int_refColorArray;
        Terraria.On_Lighting.GetColor4Slice_int_int_refColorArray += _GetColor4Slice_int_int_refColorArray;
        Terraria.GameContent.Drawing.On_TileDrawing.PostDrawTiles += _PostDrawTiles;
        Terraria.On_Main.DrawSurfaceBG += _DrawSurfaceBG;
        Terraria.On_Main.RenderWater += _RenderWater;
        Terraria.On_Main.DrawWaters += _DrawWaters;
        Terraria.On_Main.RenderBackground += _RenderBackground;
        Terraria.On_Main.DrawBackground += _DrawBackground;
        Terraria.On_Main.RenderBlack += _RenderBlack;
        Terraria.On_Main.RenderTiles += _RenderTiles;
        Terraria.On_Main.RenderTiles2 += _RenderTiles2;
        Terraria.On_Main.RenderWalls += _RenderWalls;
        Terraria.Graphics.Light.On_LightingEngine.ProcessBlur += _ProcessBlur;
        Terraria.Graphics.Light.On_LightMap.Blur += _Blur;
        // Camera mode hooks added below
        // For some reason the order in which these are added matters to ensure that camera mode works
        // Maybe DrawCapture needs to be added last
        Terraria.On_Main.DrawLiquid += _DrawLiquid;
        Terraria.On_Main.DrawWalls += _DrawWalls;
        Terraria.On_Main.DrawTiles += _DrawTiles;
        Terraria.On_Main.DrawCapture += _DrawCapture;
    }

    private static Vector3 _GetSubLight(
        Terraria.On_Lighting.orig_GetSubLight orig,
        Vector2 position
    )
    {
        if (!_overrideLightingColor)
        {
            return orig(position);
        }

        return Vector3.One;
    }

    private static void _GetCornerColors(
        Terraria.On_Lighting.orig_GetCornerColors orig,
        int centerX,
        int centerY,
        out VertexColors vertices,
        float scale
    )
    {
        if (!_overrideLightingColor)
        {
            orig(centerX, centerY, out vertices, scale);
            return;
        }

        vertices = new(Color.White);
    }

    private static void _GetColor9Slice_int_int_refVector3Array(
       Terraria.On_Lighting.orig_GetColor9Slice_int_int_refVector3Array orig,
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

        for (int i = 0; i < slices.Length; ++i)
        {
            ref Vector3 slice = ref slices[i];
            slice.X = 1f;
            slice.Y = 1f;
            slice.Z = 1f;
        }
    }

    private static void _GetColor4Slice_int_int_refVector3Array(
        Terraria.On_Lighting.orig_GetColor4Slice_int_int_refVector3Array orig,
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

        for (int i = 0; i < slices.Length; ++i)
        {
            ref Vector3 slice = ref slices[i];
            slice.X = 1f;
            slice.Y = 1f;
            slice.Z = 1f;
        }
    }

    private static void _GetColor9Slice_int_int_refColorArray(
       Terraria.On_Lighting.orig_GetColor9Slice_int_int_refColorArray orig,
       int x,
       int y,
       ref Color[] slices
    )
    {
        if (!_overrideLightingColor)
        {
            orig(x, y, ref slices);
            return;
        }

        for (int i = 0; i < slices.Length; ++i)
        {
            slices[i].PackedValue = 0xFFFFFFFF; // White
        }
    }

    private static void _GetColor4Slice_int_int_refColorArray(
        Terraria.On_Lighting.orig_GetColor4Slice_int_int_refColorArray orig,
        int x,
        int y,
        ref Color[] slices
    )
    {
        if (!_overrideLightingColor)
        {
            orig(x, y, ref slices);
            return;
        }

        for (int i = 0; i < slices.Length; ++i)
        {
            slices[i].PackedValue = 0xFFFFFFFF; // White
        }
    }

    // Tile entities
    private void _PostDrawTiles(
        Terraria.GameContent.Drawing.On_TileDrawing.orig_PostDrawTiles orig,
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

        if (_inCameraMode)
        {
            Main.instance.GraphicsDevice.SetRenderTarget(
                _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
            );
            Main.instance.GraphicsDevice.Clear(Color.Transparent);
            _PostDrawTiles_inner(orig, self, solidLayer, forRenderTargets, intoRenderTargets);

            _smoothLightingInstance.CalculateSmoothLighting(false, true);
            _smoothLightingInstance.DrawSmoothLightingCameraMode(
                _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, false, false, true, true
            );

            return;
        }

        RenderTarget2D target = MainRenderTarget.Get();
        if (target is null)
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        TextureMaker.MakeSize(ref _screenTarget1, target.Width, target.Height);
        TextureMaker.MakeSize(ref _screenTarget2, target.Width, target.Height);

        Main.instance.GraphicsDevice.SetRenderTarget(_screenTarget1);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(target, Vector2.Zero, Color.White);
        Main.spriteBatch.End();

        Main.instance.GraphicsDevice.SetRenderTarget(_screenTarget2);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        _PostDrawTiles_inner(orig, self, solidLayer, forRenderTargets, intoRenderTargets);

        _smoothLightingInstance.CalculateSmoothLighting(false, false);
        _smoothLightingInstance.DrawSmoothLighting(_screenTarget2, false, true, target);

        Main.instance.GraphicsDevice.SetRenderTarget(target);
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        Main.spriteBatch.Draw(_screenTarget1, Vector2.Zero, Color.White);
        Main.spriteBatch.Draw(_screenTarget2, Vector2.Zero, Color.White);
        Main.spriteBatch.End();
    }

    private void _PostDrawTiles_inner(
        Terraria.GameContent.Drawing.On_TileDrawing.orig_PostDrawTiles orig,
        Terraria.GameContent.Drawing.TileDrawing self,
        bool solidLayer,
        bool forRenderTargets,
        bool intoRenderTargets
    )
    {
        if (solidLayer
            || intoRenderTargets
            || !LightingConfig.Instance.UseGammaCorrection())
        {
            orig(self, solidLayer, forRenderTargets, intoRenderTargets);
            return;
        }

        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            Main.DefaultSamplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.Transform
        );

        _smoothLightingInstance.ApplyGammaCorrectionShader();

        method_DrawMultiTileVines(self);
        method_DrawMultiTileGrass(self);
        method_DrawVoidLenses(self);
        method_DrawTeleportationPylons(self);
        method_DrawMasterTrophies(self);
        method_DrawGrass(self);
        method_DrawAnyDirectionalGrass(self);
        method_DrawTrees(self);
        method_DrawVines(self);
        method_DrawReverseVines(self);

        Main.spriteBatch.End();
    }

    private void _DrawSurfaceBG(
        Terraria.On_Main.orig_DrawSurfaceBG orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.UseGammaCorrection() || LightingConfig.Instance.RenderOnlyLight)
        {
            orig(self);
            return;
        }

        Matrix transform;
        if (_inCameraMode)
        {
            transform = Main.Transform;
        }
        else
        {
            transform = Main.BackgroundViewMatrix.TransformationMatrix;
            transform.Translation
                -= Main.BackgroundViewMatrix.ZoomMatrix.Translation
                * new Vector3(
                    1f,
                    Main.BackgroundViewMatrix.Effects.HasFlag(SpriteEffects.FlipVertically)
                        ? -1f
                        : 1f,
                    1f
                );
        }

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.AnisotropicClamp,
            DepthStencilState.Default,
            RasterizerState.CullNone,
            null,
            transform
        );

        _smoothLightingInstance.ApplyGammaCorrectionBGShader();
        orig(self);
    }

    private void _RenderWater(
        Terraria.On_Main.orig_RenderWater orig,
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

        if (!LightingConfig.Instance.SmoothLightingEnabled())
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
        Terraria.On_Main.orig_DrawWaters orig,
        Terraria.Main self,
        bool isBackground
    )
    {
        if (_inCameraMode || !LightingConfig.Instance.SmoothLightingEnabled())
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
        Terraria.On_Main.orig_RenderBackground orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
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
        _smoothLightingInstance.DrawSmoothLighting(Main.instance.backWaterTarget, true, true);
    }

    private void _DrawBackground(
        Terraria.On_Main.orig_DrawBackground orig,
        Terraria.Main self
    )
    {
        if (!LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self);
            return;
        }

        if (_inCameraMode)
        {
            _smoothLightingInstance.CalculateSmoothLighting(true, true);
            OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;

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
        Terraria.On_Main.orig_RenderBlack orig,
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
        Terraria.On_Main.orig_RenderTiles orig,
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
        Terraria.On_Main.orig_RenderTiles2 orig,
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
        Terraria.On_Main.orig_RenderWalls orig,
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
        Terraria.Graphics.Light.On_LightingEngine.orig_ProcessBlur orig,
        Terraria.Graphics.Light.LightingEngine self
    )
    {
        if (!LightingConfig.Instance.FancyLightingEngineEnabled())
        {
            orig(self);
            return;
        }

        _fancyLightingEngineInstance.SetLightMapArea((Rectangle)field_workingProcessedArea.GetValue(self));
        orig(self);
    }

    private void _Blur(
        Terraria.Graphics.Light.On_LightMap.orig_Blur orig,
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
            _fancyLightingEngineInstance.SpreadLight(
                self, colors, lightDecay, self.Width, self.Height
            );
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

    // Camera mode hooks below

    private void _DrawLiquid(
        Terraria.On_Main.orig_DrawLiquid orig,
        Terraria.Main self,
        bool bg,
        int Style,
        float Alpha,
        bool drawSinglePassLiquids
    )
    {
        if (!_inCameraMode || !LightingConfig.Instance.SmoothLightingEnabled())
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
            return;
        }

        _smoothLightingInstance.CalculateSmoothLighting(bg, true);
        OverrideLightingColor = true;

        Main.spriteBatch.End();
        Main.instance.GraphicsDevice.SetRenderTarget(
            _smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget)
        );
        Main.instance.GraphicsDevice.Clear(Color.Transparent);
        Main.spriteBatch.Begin();
        try
        {
            orig(self, bg, Style, Alpha, drawSinglePassLiquids);
        }
        finally
        {
            OverrideLightingColor = false;
        }
        Main.spriteBatch.End();

        _smoothLightingInstance.DrawSmoothLightingCameraMode(
            _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, bg, false, true
        );

        Main.spriteBatch.Begin();
    }

    private void _DrawWalls(
        Terraria.On_Main.orig_DrawWalls orig,
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
        Terraria.On_Main.orig_DrawTiles orig,
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
        Terraria.On_Main.orig_DrawCapture orig,
        Terraria.Main self,
        Rectangle area,
        CaptureSettings settings
    )
    {
        if (LightingConfig.Instance.ModifyCameraModeRendering())
        {
            _cameraModeTarget = MainRenderTarget.Get();
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
