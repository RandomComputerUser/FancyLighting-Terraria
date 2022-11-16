using FancyLighting.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting
{
    public class FancyLightingMod : Mod
    {
        public static BlendState MultiplyBlend { get; private set; }

        internal static bool _smoothLightingEnabled;
        internal static bool _blurLightMap;
        internal static Config.RenderMode _lightMapRenderMode;
        internal static int _normalMapsStrength;
        internal static bool _useFineNormalMaps;
        internal static bool _renderOnlyLight;

        internal static bool _ambientOcclusionEnabled;
        internal static bool _ambientOcclusionNonSolid;
        internal static bool _ambientOcclusionTileEntity;
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

        private static RenderTarget2D _screenTarget1;
        private static RenderTarget2D _screenTarget2;

        public bool OverrideLightingColor
        {
            get
            {
                return _overrideLightingColor;
            }
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

                object lightingEngineInstance = field_activeEngine.GetValue(null);
                if (lightingEngineInstance.GetType() != typeof(LightingEngine))
                {
                    return;
                }

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

        public static bool UseBicubicScaling
        {
            get
            {
                return _lightMapRenderMode != Config.RenderMode.Bilinear;
            }
        }

        public static bool DrawOverbright
        {
            get
            {
                return _lightMapRenderMode == Config.RenderMode.BicubicOverbright;
            }
        }

        public static bool SimulateNormalMaps
        {
            get
            {
                return _normalMapsStrength != 0;
            }
        }

        public static bool UseFineNormalMaps
        {
            get
            {
                return _useFineNormalMaps;
            }
        }

        public static float NormalMapsStrength
        {
            get
            {
                return _normalMapsStrength / 100f;
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

        public static bool DoNonSolidAmbientOcclusion
        {
            get
            {
                return _ambientOcclusionNonSolid;
            }
        }

        public static bool DoTileEntityAmbientOcclusion
        {
            get
            {
                return _ambientOcclusionTileEntity;
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
            if (Main.netMode == NetmodeID.Server)
            {
                return;
            }

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
        }

        public override void Unload()
        {
            Main.QueueMainThreadAction(
                () =>
                {
                    _cameraModeTarget?.Dispose();
                    _screenTarget1?.Dispose();
                    _screenTarget2?.Dispose();
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
                {
                    slices[i] = Vector3.One;
                }
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
                {
                    slices[i] = Vector3.One;
                }
            };

            // Tile entities
            On.Terraria.GameContent.Drawing.TileDrawing.PostDrawTiles +=
            (
                On.Terraria.GameContent.Drawing.TileDrawing.orig_PostDrawTiles orig,
                Terraria.GameContent.Drawing.TileDrawing self,
                bool solidLayer,
                bool forRenderTargets,
                bool intoRenderTargets
            ) =>
            {
                if (intoRenderTargets
                    || !DrawOverbright
                    || !SmoothLightingEnabled
                    || RenderOnlyLight
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

                TextureSize.MakeSize(ref _screenTarget1, target.Width, target.Height);
                TextureSize.MakeSize(ref _screenTarget2, target.Width, target.Height);

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
            };

            On.Terraria.Main.RenderWater +=
            (
                On.Terraria.Main.orig_RenderWater orig,
                Terraria.Main self
            ) =>
            {
                if (RenderOnlyLight && SmoothLightingEnabled)
                {
                    Main.instance.GraphicsDevice.SetRenderTarget(Main.waterTarget);
                    Main.instance.GraphicsDevice.Clear(Color.Transparent);
                    Main.instance.GraphicsDevice.SetRenderTarget(null);
                    return;
                }

                if (!DrawOverbright || !SmoothLightingEnabled)
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
            };

            On.Terraria.Main.DrawWaters +=
            (
                On.Terraria.Main.orig_DrawWaters orig,
                Terraria.Main self,
                bool isBackground
            ) =>
            {
                if (_inCameraMode || !DrawOverbright || !SmoothLightingEnabled)
                {
                    orig(self, isBackground);
                    return;
                }
                if (isBackground)
                {
                    OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
                }
                else
                {
                    OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
                }
                orig(self, isBackground);
                OverrideLightingColor = false;
            };

            // Cave backgrounds
            On.Terraria.Main.RenderBackground +=
            (
                On.Terraria.Main.orig_RenderBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled || !(FancyLightingEngineEnabled || DrawOverbright))
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
                if (DrawOverbright)
                {
                    _smoothLightingInstance.DrawSmoothLighting(Main.instance.backWaterTarget, true, true);
                }
            };

            On.Terraria.Main.DrawBackground +=
            (
                On.Terraria.Main.orig_DrawBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled || !(FancyLightingEngineEnabled || DrawOverbright))
                {
                    orig(self);
                    return;
                }

                if (_inCameraMode)
                {
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
                        _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, true, false, true
                    );

                    Main.tileBatch.Begin();
                    Main.spriteBatch.Begin();
                }
                else
                {
                    OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
                    orig(self);
                    OverrideLightingColor = false;
                }
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
                if (!SmoothLightingEnabled)
                {
                    orig(self);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(false);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
                orig(self);
                OverrideLightingColor = false;
                if (Main.drawToScreen)
                {
                    return;
                }

                _smoothLightingInstance.DrawSmoothLighting(Main.instance.tileTarget, false);
            };

            On.Terraria.Main.RenderTiles2 +=
            (
                On.Terraria.Main.orig_RenderTiles2 orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled)
                {
                    orig(self);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(false);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingFore;
                orig(self);
                OverrideLightingColor = false;
                if (Main.drawToScreen)
                {
                    return;
                }

                _smoothLightingInstance.DrawSmoothLighting(Main.instance.tile2Target, false);
            };

            On.Terraria.Main.RenderWalls +=
            (
                On.Terraria.Main.orig_RenderWalls orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled)
                {
                    orig(self);
                    return;
                }

                _smoothLightingInstance.CalculateSmoothLighting(true);
                OverrideLightingColor = _smoothLightingInstance.DrawSmoothLightingBack;
                orig(self);
                OverrideLightingColor = false;
                if (Main.drawToScreen)
                {
                    return;
                }

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
                {
                    _fancyLightingEngineInstance.SpreadLight(self, colors, lightDecay, self.Width, self.Height);
                }
                else
                {
                    orig(self);
                }

                if (SmoothLightingEnabled)
                {
                    _smoothLightingInstance.GetAndBlurLightMap(colors, self.Width, self.Height);
                }
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
                    _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, false, false
                );

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            };

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
                    _cameraModeTarget, _smoothLightingInstance._cameraModeTarget1, true, AmbientOcclusionEnabled);

                if (AmbientOcclusionEnabled)
                {
                    _ambientOcclusionInstance.ApplyAmbientOcclusionCameraMode(
                        _cameraModeTarget, _smoothLightingInstance._cameraModeTarget2, _cameraModeBiome);
                }

                Main.tileBatch.Begin();
                Main.spriteBatch.Begin();
            };
        }
    }
}
