using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Light;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Reflection;

namespace FancyLighting
{
    public class FancyLightingMod : Mod
    {
        internal static BlendState MultiplyBlend;

        internal static bool _smoothLightingEnabled;
        internal static bool _blurLightMap;
        internal static bool _customUpscalingEnabled;
        internal static bool _renderOnlyLight;

        internal static bool _ambientOcclusionEnabled;
        internal static int _ambientOcclusionRadius;
        internal static int _ambientOcclusionIntensity;

        internal static bool _fancyLightingEngineEnabled;
        internal static bool _fancyLightingEngineUseTemporal;
        internal static int _fancyLightingEngineLightLoss;
        internal static bool _fancyLightingEngineMakeBrighter;

        internal static int _threadCount;


        internal static bool _overrideLightingColor;
        internal static bool _overrideFastRandom;
        internal static int _mushroomDustCount;

        internal FancyLightingEngine FancyLightingEngineObj;
        internal SmoothLighting SmoothLightingObj;
        internal AmbientOcclusion AmbientOcclusionObj;

        internal FieldInfo field_activeEngine;
        internal FieldInfo field_workingProcessedArea;
        internal FieldInfo field_colors;
        internal FieldInfo field_mask;


        public static bool SmoothLightingEnabled
        {
            get
            {
                return _smoothLightingEnabled;
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
            _overrideFastRandom = false;
            _mushroomDustCount = 0;

            ModContent.GetInstance<FancyLightingModSystem>().UpdateSettings();

            BlendState blend = new BlendState();
            blend.ColorBlendFunction = BlendFunction.Add;
            blend.ColorSourceBlend = Blend.Zero;
            blend.ColorDestinationBlend = Blend.SourceColor;
            MultiplyBlend = blend;

            SmoothLightingObj = new SmoothLighting(this);
            AmbientOcclusionObj = new AmbientOcclusion();
            FancyLightingEngineObj = new FancyLightingEngine();

            field_activeEngine = typeof(Lighting).GetField("_activeEngine", BindingFlags.NonPublic | BindingFlags.Static);
            field_workingProcessedArea = typeof(LightingEngine).GetField("_workingProcessedArea", BindingFlags.NonPublic | BindingFlags.Instance);
            field_colors = typeof(LightMap).GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance);
            field_mask = typeof(LightMap).GetField("_mask", BindingFlags.NonPublic | BindingFlags.Instance);

            AddHooks();
        }

        public override void Unload()
        {
            try
            {
                SmoothLightingObj.Unload();
                AmbientOcclusionObj.Unload();
            }
            catch (Exception ex)
            {
                // Ignore
            }

            base.Unload();
        }

        protected void AddHooks()
        {
            On.Terraria.Graphics.Light.LightingEngine.GetColor +=
            (
                On.Terraria.Graphics.Light.LightingEngine.orig_GetColor orig,
                Terraria.Graphics.Light.LightingEngine self,
                int x,
                int y
            ) =>
            {
                if (_overrideLightingColor)
                {
                    Vector3 color = orig(self, x, y);
                    if (color.X < 1f / 255f && color.Y < 1f / 255f && color.Z < 1f / 255f)
                    {
                        color.X = 1f / 255f;
                        color.Y = 1f / 255f;
                        color.Z = 1f / 255f;
                        return color;
                    }
                    color.X = 1f;
                    color.Y = 1f;
                    color.Z = 1f;
                    return color;
                }
                return orig(self, x, y);
            };

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
                SmoothLightingObj.CalculateSmoothLighting(true);
                orig(self);
                if (Main.drawToScreen)
                    return;
                SmoothLightingObj.DrawSmoothLighting(Main.instance.backgroundTarget, true);
            };

            On.Terraria.Main.DrawBackground +=
            (
                On.Terraria.Main.orig_DrawBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!SmoothLightingEnabled || !FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }
                _overrideLightingColor = SmoothLightingObj.DrawSmoothLightingBack;
                orig(self);
                _overrideLightingColor = false;
            };

            On.Terraria.Main.RenderBlack +=
            (
                On.Terraria.Main.orig_RenderBlack orig,
                Terraria.Main self
            ) =>
            {

                bool initialLightingOverride = _overrideLightingColor;
                _overrideLightingColor = false;
                orig(self);
                _overrideLightingColor = initialLightingOverride;
            };
            
            On.Terraria.Main.RenderTiles +=
            (
                On.Terraria.Main.orig_RenderTiles orig,
                Terraria.Main self
            ) =>
            {
                SmoothLightingObj.CalculateSmoothLighting(false);
                _overrideLightingColor = SmoothLightingObj.DrawSmoothLightingFore;
                orig(self);
                _overrideLightingColor = false;
                SmoothLightingObj.DrawSmoothLighting(Main.instance.tileTarget, false);
            };

            On.Terraria.Main.RenderTiles2 +=
            (
                On.Terraria.Main.orig_RenderTiles2 orig,
                Terraria.Main self
            ) =>
            {
                SmoothLightingObj.CalculateSmoothLighting(false);
                _overrideLightingColor = SmoothLightingObj.DrawSmoothLightingFore;
                orig(self);
                _overrideLightingColor = false;
                SmoothLightingObj.DrawSmoothLighting(Main.instance.tile2Target, false);
            };

            On.Terraria.Main.RenderWalls +=
            (
                On.Terraria.Main.orig_RenderWalls orig,
                Terraria.Main self
            ) =>
            {
                SmoothLightingObj.CalculateSmoothLighting(true);
                _overrideLightingColor = SmoothLightingObj.DrawSmoothLightingBack;
                orig(self);
                _overrideLightingColor = false;
                if (Main.drawToScreen)
                    return;
                SmoothLightingObj.DrawSmoothLighting(Main.instance.wallTarget, true);
                AmbientOcclusionObj.ApplyAmbientOcclusion();
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

                FancyLightingEngineObj.lightMapArea = 
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
                    FancyLightingEngineObj.SpreadLight(self, colors, lightDecay, self.Width, self.Height);
                else
                    orig(self);
                if (SmoothLightingEnabled)
                    SmoothLightingObj.BlurLightMap(colors, self.Width, self.Height);
            };
        }
    }
}
