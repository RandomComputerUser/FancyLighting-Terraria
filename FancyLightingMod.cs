using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

using System;
using System.Reflection;

namespace FancyLighting
{
    public class FancyLightingMod : Mod
    {
        public static BlendState MultiplyBlend { get; private set; }

        internal static bool _smoothLightingEnabled;
        internal static bool _blurLightMap;
        internal static bool _customUpscalingEnabled;
        internal static bool _renderOnlyLight;

        internal static bool _ambientOcclusionEnabled;
        internal static bool _ambientOcclusionExtra;
        internal static int _ambientOcclusionRadius;
        internal static int _ambientOcclusionIntensity;

        internal static bool _fancyLightingEngineEnabled;
        internal static bool _fancyLightingEngineUseTemporal;
        internal static int _fancyLightingEngineLightLoss;
        internal static bool _fancyLightingEngineMakeBrighter;

        internal static bool _skyColorsEnabled;

        internal static int _threadCount;

        internal static bool _overrideLightingColor;
        internal static bool _inCameraMode;

        internal FancyLightingEngine _fancyLightingEngineInstance;
        internal SmoothLighting _smoothLightingInstance;
        internal AmbientOcclusion _ambientOcclusionInstance;

        internal FieldInfo field_activeEngine;
        internal FieldInfo field_activeLightMap;
        internal FieldInfo field_workingProcessedArea;
        internal FieldInfo field_colors;
        internal FieldInfo field_mask;

        private static RenderTarget2D _cameraModeTarget;
        internal static Rectangle _cameraModeArea;
        internal static CaptureBiome _cameraModeBiome;

        public bool OverrideLightingColor
        {
            get
            {
                return _overrideLightingColor;
            }
            internal set
            {
                if (value == _overrideLightingColor)
                    return;
                if (!Lighting.UsingNewLighting)
                    return;

                object lightingEngineInstance = field_activeEngine.GetValue(null);
                if (lightingEngineInstance.GetType() != typeof(LightingEngine))
                    return;

                LightMap lightMapInstance = (LightMap)field_activeLightMap.GetValue((LightingEngine)lightingEngineInstance);

                if (value)
                {
                    _smoothLightingInstance._tmpLights = (Vector3[])field_colors.GetValue(lightMapInstance);
                    field_colors.SetValue(lightMapInstance, _smoothLightingInstance._whiteLights);
                }
                else
                {
                    field_colors.SetValue(lightMapInstance, _smoothLightingInstance._tmpLights);
                }

                _overrideLightingColor = value;
            }
        }

        public static bool ModifyCameraModeRendering
        {
            get
            {
                return Lighting.UsingNewLighting && (SmoothLightingEnabled || AmbientOcclusionEnabled);
            }
        }

        public static bool SmoothLightingEnabled
        {
            get
            {
                return _smoothLightingEnabled && Lighting.UsingNewLighting;
            }
        }

        public static bool BlurLightMap
        {
            get
            {
                return _blurLightMap;
            }
        }

        public static bool CustomUpscalingEnabled
        {
            get
            {
                return _customUpscalingEnabled;
            }
        }

        public static bool RenderOnlyLight
        {
            get
            {
                return _renderOnlyLight;
            }
        }

        public static bool AmbientOcclusionEnabled
        {
            get
            {
                return _ambientOcclusionEnabled;
            }
        }

        public static bool AmbientOcclusionFromExtraTiles
        {
            get
            {
                return _ambientOcclusionExtra;
            }
        }

        public static int AmbientOcclusionRadius
        {
            get
            {
                return _ambientOcclusionRadius;
            }
        }

        public static float AmbientOcclusionIntensity
        {
            get
            {
                return (100 - _ambientOcclusionIntensity) / 100f;
            }
        }

        public static bool FancyLightingEngineEnabled
        {
            get
            {
                return _fancyLightingEngineEnabled && Lighting.UsingNewLighting;
            }
        }

        public static bool FancyLightingEngineUseTemporal
        {
            get
            {
                return _fancyLightingEngineUseTemporal;
            }
        }

        public static float FancyLightingEngineLightLoss
        {
            get
            {
                return (100 - _fancyLightingEngineLightLoss) / 100f;
            }
        }

        public static bool FancyLightingEngineMakeBrighter
        {
            get
            {
                return _fancyLightingEngineMakeBrighter;
            }
        }

        public static bool CustomSkyColorsEnabled
        {
            get
            {
                return _skyColorsEnabled;
            }
        }

        public static int ThreadCount
        {
            get
            {
                return _threadCount;
            }
        }

        public override void Load()
        {
            if (Main.netMode == NetmodeID.Server) return;

            _overrideLightingColor = false;
            _inCameraMode = false;

            ModContent.GetInstance<FancyLightingModSystem>()?.UpdateSettings();

            BlendState blend = new BlendState();
            blend.ColorBlendFunction = BlendFunction.Add;
            blend.ColorSourceBlend = Blend.Zero;
            blend.ColorDestinationBlend = Blend.SourceColor;
            MultiplyBlend = blend;

            _smoothLightingInstance = new SmoothLighting(this);
            _ambientOcclusionInstance = new AmbientOcclusion();
            _fancyLightingEngineInstance = new FancyLightingEngine();

            field_activeEngine = typeof(Lighting).GetField("_activeEngine", BindingFlags.NonPublic | BindingFlags.Static);
            field_activeLightMap = typeof(LightingEngine).GetField("_activeLightMap", BindingFlags.NonPublic | BindingFlags.Instance);
            field_workingProcessedArea = typeof(LightingEngine).GetField("_workingProcessedArea", BindingFlags.NonPublic | BindingFlags.Instance);
            field_colors = typeof(LightMap).GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance);
            field_mask = typeof(LightMap).GetField("_mask", BindingFlags.NonPublic | BindingFlags.Instance);

            AddHooks();
            SkyColors.AddSkyColorsHooks();
            AddCameraModeHooks();
        }

        public override void Unload()
        {
            Main.QueueMainThreadAction(
                () => 
                {
                    _cameraModeTarget = null;
                    _smoothLightingInstance?.Unload();
                    _ambientOcclusionInstance?.Unload();
                }
            );

            base.Unload();
        }

        protected void AddHooks()
        {
            On.Terraria.Lighting.GetColor9Slice_int_int_refVector3Array +=
            (
                On.Terraria.Lighting.orig_GetColor9Slice_int_int_refVector3Array orig,
                int x,
                int y,
                ref Vector3[] slices
            ) =>
            {
                if (!_overrideLightingColor)
                {
                    orig(x, y, ref slices);
                    return;
                }
                for (int i = 0; i < 9; ++i)
                    slices[i] = Vector3.One;
            };
            
            On.Terraria.Lighting.GetColor4Slice_int_int_refVector3Array +=
            (
                On.Terraria.Lighting.orig_GetColor4Slice_int_int_refVector3Array orig,
                int x,
                int y,
                ref Vector3[] slices
            ) =>
            {
                if (!_overrideLightingColor)
                {
                    orig(x, y, ref slices);
                    return;
                }
                for (int i = 0; i < 4; ++i)
                    slices[i] = Vector3.One;
            };

            // Cave backgrounds
            On.Terraria.Main.RenderBackground +=
            (
                On.Terraria.Main.orig_RenderBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled || !FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }
                _smoothLightingInstance.CalculateSmoothLighting(true);
                orig(self);
                if (Main.drawToScreen)
                    return;
                _smoothLightingInstance.DrawSmoothLighting(Main.instance.backgroundTarget, true);
            };

            On.Terraria.Main.DrawBackground +=
            (
                On.Terraria.Main.orig_DrawBackground orig,
                Terraria.Main self
            ) =>
            {
                if (_inCameraMode || !SmoothLightingEnabled || !FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
                orig(self);
                OverrideLightingColor = false;
            };

            On.Terraria.Main.RenderBlack +=
            (
                On.Terraria.Main.orig_RenderBlack orig,
                Terraria.Main self
            ) =>
            {
                bool initialLightingOverride = OverrideLightingColor;
                OverrideLightingColor = false;
                orig(self);
                OverrideLightingColor = initialLightingOverride;
            };
            
            On.Terraria.Main.RenderTiles +=
            (
                On.Terraria.Main.orig_RenderTiles orig,
                Terraria.Main self
            ) =>
            {
                _smoothLightingInstance.CalculateSmoothLighting(false);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
                orig(self);
                OverrideLightingColor = false;
                _smoothLightingInstance.DrawSmoothLighting(Main.instance.tileTarget, false);
            };

            On.Terraria.Main.RenderTiles2 +=
            (
                On.Terraria.Main.orig_RenderTiles2 orig,
                Terraria.Main self
            ) =>
            {
                _smoothLightingInstance.CalculateSmoothLighting(false);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
                orig(self);
                OverrideLightingColor = false;
                _smoothLightingInstance.DrawSmoothLighting(Main.instance.tile2Target, false);
            };

            On.Terraria.Main.RenderWalls +=
            (
                On.Terraria.Main.orig_RenderWalls orig,
                Terraria.Main self
            ) =>
            {
                _smoothLightingInstance.CalculateSmoothLighting(true);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
                orig(self);
                OverrideLightingColor = false;
                if (Main.drawToScreen)
                    return;
                _smoothLightingInstance.DrawSmoothLighting(Main.instance.wallTarget, true);
                _ambientOcclusionInstance.ApplyAmbientOcclusion();
            };

            On.Terraria.Graphics.Light.LightingEngine.ProcessBlur +=
            (
                On.Terraria.Graphics.Light.LightingEngine.orig_ProcessBlur orig,
                Terraria.Graphics.Light.LightingEngine self
            ) =>
            {
                if (!FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }

                _fancyLightingEngineInstance._lightMapArea = 
                    (Rectangle)field_workingProcessedArea.GetValue(self);
                orig(self);
            };

            On.Terraria.Graphics.Light.LightMap.Blur +=
            (
                On.Terraria.Graphics.Light.LightMap.orig_Blur orig,
                Terraria.Graphics.Light.LightMap self
            ) =>
            {
                if (!SmoothLightingEnabled && !FancyLightingEngineEnabled)
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
                if (FancyLightingEngineEnabled)
                    _fancyLightingEngineInstance.SpreadLight(self, colors, lightDecay, self.Width, self.Height);
                else
                    orig(self);
                if (SmoothLightingEnabled)
                    _smoothLightingInstance.GetAndBlurLightMap(colors, self.Width, self.Height);
            };
        }

        protected void AddCameraModeHooks()
        {
            On.Terraria.Main.DrawWalls +=
            (
                On.Terraria.Main.orig_DrawWalls orig,
                Terraria.Main self
            ) =>
            {
                if (!_inCameraMode)
                {
                    orig(self);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(true, true);
                OverrideLightingColor = SmoothLightingEnabled;

                Main.tileBatch.End();
                Main.spriteBatch.End();
                Main.instance.GraphicsDevice.SetRenderTarget(_smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget));
                Main.instance.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(self);
                Main.tileBatch.End();
                Main.spriteBatch.End();

                OverrideLightingColor = false;
                _smoothLightingInstance.DrawSmoothLightingCameraMode(
                    _cameraModeTarget, _smoothLightingInstance._cameraModeSurface, true, AmbientOcclusionEnabled);

                if (AmbientOcclusionEnabled)
                {
                    _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                        _cameraModeTarget, _smoothLightingInstance._cameraModeSurface2, _cameraModeBiome);
                }

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            };

            On.Terraria.Main.DrawTiles +=
            (
                On.Terraria.Main.orig_DrawTiles orig,
                Terraria.Main self,
                bool solidLayer,
                bool forRenderTargets,
                bool intoRenderTargets,
                int waterStyleOverride
            ) =>
            {
                if (!_inCameraMode)
                {
                    orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(false, true);
                OverrideLightingColor = SmoothLightingEnabled;

                Main.tileBatch.End();
                Main.spriteBatch.End();
                Main.instance.GraphicsDevice.SetRenderTarget(_smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget));
                Main.instance.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
                Main.tileBatch.End();
                Main.spriteBatch.End();

                OverrideLightingColor = false;
                _smoothLightingInstance.DrawSmoothLightingCameraMode(
                    _cameraModeTarget, _smoothLightingInstance._cameraModeSurface, false, false);

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            };

            On.Terraria.Main.DrawCapture +=
            (
                On.Terraria.Main.orig_DrawCapture orig,
                Terraria.Main self,
                Rectangle area,
                CaptureSettings settings
            ) =>
            {
                var renderTargets = Main.instance.GraphicsDevice.GetRenderTargets();
                if (renderTargets is null || renderTargets.Length < 1)
                {
                    _cameraModeTarget = null;
                }
                else
                {
                    _cameraModeTarget = (RenderTarget2D)renderTargets[0].RenderTarget;
                }
                _inCameraMode = ModifyCameraModeRendering && (_cameraModeTarget is not null);
                if (_inCameraMode)
                {
                    _cameraModeArea = area;
                    _cameraModeBiome = settings.Biome;
                }
                orig(self, area, settings);
                _inCameraMode = false;
            };

            On.Terraria.Main.DrawBackground +=
            (
                On.Terraria.Main.orig_DrawBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!_inCameraMode || !SmoothLightingEnabled || !FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(true, true);
                OverrideLightingColor = SmoothLightingEnabled;

                Main.tileBatch.End();
                Main.spriteBatch.End();
                Main.instance.GraphicsDevice.SetRenderTarget(_smoothLightingInstance.GetCameraModeRenderTarget(_cameraModeTarget));
                Main.instance.GraphicsDevice.Clear(Color.Transparent);
                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
                orig(self);
                Main.tileBatch.End();
                Main.spriteBatch.End();

                OverrideLightingColor = false;
                _smoothLightingInstance.DrawSmoothLightingCameraMode(
                    _cameraModeTarget, _smoothLightingInstance._cameraModeSurface, true, false);

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            };
        }
    }
}
