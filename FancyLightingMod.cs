using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Light;

using System.Reflection;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FancyLighting
{
    public class FancyLightingMod : Mod
    {
        internal static BlendState MultiplyBlend;

        internal static bool _smoothLightingEnabled;
        internal static bool _ambientOcclusionEnabled;
        internal static int _ambientOcclusionRadius;
        internal static int _ambientOcclusionIntensity;
        internal static bool _fancyLightingEngineEnabled;
        internal static int _fancyLightingEngineThreadCount;
        internal static bool _fancyLightingEngineUseTemporal;
        internal static int _fancyLightingEngineLightLoss;

        internal static bool _overrideLightingColor;
        internal static bool _currentlyOverridingWallLighting;
        internal static bool _overrideFastRandom;
        internal static int _mushroomDustCount;

        internal FancyLightingEngine FancyLightingEngineObj;
        internal SmoothLighting SmoothLightingObj;
        internal AmbientOcclusion AmbientOcclusionObj;

        protected FieldInfo field_workingProcessedArea;
        protected FieldInfo field_colors;
        protected FieldInfo field_mask;

        public static bool SmoothLightingEnabled
        {
            get
            {
                return _smoothLightingEnabled;
            }
        }

        public static bool AmbientOcclusionEnabled
        {
            get
            {
                return _ambientOcclusionEnabled;
            }
        }

        public static bool OverrideLightingColor
        {
            get
            {
                return SmoothLightingEnabled && _overrideLightingColor;
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

        public static int FancyLightingEngineThreadCount
        {
            get
            {
                return _fancyLightingEngineThreadCount;
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

        public override void Load()
        {
            if (Main.netMode == NetmodeID.Server) return;

            _overrideLightingColor = false;
            _currentlyOverridingWallLighting = false;
            _overrideFastRandom = false;
            _mushroomDustCount = 0;

            FancyLightingModSystem.UpdateSettings();

            BlendState blend = new BlendState();
            blend.ColorBlendFunction = BlendFunction.Add;
            blend.ColorSourceBlend = Blend.Zero;
            blend.ColorDestinationBlend = Blend.SourceColor;
            MultiplyBlend = blend;

            SmoothLightingObj = new SmoothLighting();
            AmbientOcclusionObj = new AmbientOcclusion();
            FancyLightingEngineObj = new FancyLightingEngine();

            field_workingProcessedArea = typeof(LightingEngine).GetField("_workingProcessedArea", BindingFlags.NonPublic | BindingFlags.Instance);
            field_colors = typeof(LightMap).GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance);
            field_mask = typeof(LightMap).GetField("_mask", BindingFlags.NonPublic | BindingFlags.Instance);

            AddHooks();
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
                if (OverrideLightingColor)
                {
                    if (SmoothLighting.IsGlowingTile(x, y, _currentlyOverridingWallLighting))
                        return Vector3.One;
                    Vector3 color = orig(self, x, y);
                    if (color.X < 1 / 255f && color.Y < 1 / 255f && color.Z < 1 / 255f)
                    {
                        color.X = 1f / 255f;
                        color.Y = 1f / 255f;
                        color.Z = 1f / 255f;
                        return color;
                    }
                    return Vector3.One;
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
                if (!SmoothLightingEnabled)
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
                if (!SmoothLightingEnabled)
                {
                    orig(x, y, ref slices);
                    return;
                }
                for (int i = 0; i < 4; ++i)
                    slices[i] = Vector3.One;
            };

            On.Terraria.Main.RenderBackground +=
            (
                On.Terraria.Main.orig_RenderBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }
                orig(self);
                _overrideLightingColor = false;
                if (Main.drawToScreen)
                    return;
                if (SmoothLightingObj.CalculateSmoothLighting(false, true))
                    SmoothLightingObj.DrawSmoothLighting(Main.instance.backgroundTarget);
            };

            On.Terraria.Main.DrawBackground +=
            (
                On.Terraria.Main.orig_DrawBackground orig,
                Terraria.Main self
            ) =>
            {
                if (!FancyLightingEngineEnabled)
                {
                    orig(self);
                    return;
                }
                _overrideLightingColor = true;
                _currentlyOverridingWallLighting = false;
                orig(self);
                _overrideLightingColor = false;
            };

            On.Terraria.Main.RenderTiles +=
            (
                On.Terraria.Main.orig_RenderTiles orig,
                Terraria.Main self
            ) =>
            {
                _overrideLightingColor = true;
                _currentlyOverridingWallLighting = false;
                orig(self);
                _overrideLightingColor = false;
                if (Main.drawToScreen)
                    return;
                SmoothLightingObj.CalculateSmoothLighting(false);
                SmoothLightingObj.DrawSmoothLighting(Main.instance.tileTarget);
            };

            On.Terraria.Main.RenderWalls +=
            (
                On.Terraria.Main.orig_RenderWalls orig,
                Terraria.Main self
            ) =>
            {
                _overrideLightingColor = true;
                _currentlyOverridingWallLighting = true;
                orig(self);
                _overrideLightingColor = false;
                _currentlyOverridingWallLighting = false;
                if (Main.drawToScreen)
                    return;
                SmoothLightingObj.CalculateSmoothLighting(true);
                SmoothLightingObj.DrawSmoothLighting(Main.instance.wallTarget);
                AmbientOcclusionObj.ApplyAmbientOcclusion();
            };

            On.Terraria.Main.RenderTiles2 +=
            (
                On.Terraria.Main.orig_RenderTiles2 orig,
                Terraria.Main self
            ) =>
            {
                _overrideLightingColor = true;
                _currentlyOverridingWallLighting = false;
                orig(self);
                _overrideLightingColor = false;
                if (Main.drawToScreen)
                    return;
                SmoothLightingObj.CalculateSmoothLighting(false);
                SmoothLightingObj.DrawSmoothLighting(Main.instance.tile2Target);
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
                if (!FancyLightingEngineEnabled)
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
                FancyLightingEngineObj.SpreadLight(self, colors, lightDecay, self.Width, self.Height);
            };

            // Necessary for acceptable performance with the Fancy Lighting Engine

            On.Terraria.Graphics.Light.TileLightScanner.GetTileLight +=
            (
                On.Terraria.Graphics.Light.TileLightScanner.orig_GetTileLight orig,
                Terraria.Graphics.Light.TileLightScanner self,
                int x,
                int y,
                out Vector3 outputColor
            ) =>
            {
                _overrideFastRandom = true;
                orig(self, x, y, out outputColor);
                _overrideFastRandom = false;
            };

            On.Terraria.Utilities.FastRandom.Next_int +=
            (
                On.Terraria.Utilities.FastRandom.orig_Next_int orig,
                ref Terraria.Utilities.FastRandom self,
                int max
            ) =>
            {
                if (_overrideFastRandom) return max / 2;
                return orig(ref self, max);
            };
        }

    }
}