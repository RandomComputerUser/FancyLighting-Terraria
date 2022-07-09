using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting
{
    class FancyLightingModSystem : ModSystem
    {
        private LightingConfig _configInstance;

        FancyLightingModSystem() : base()
        {
            _configInstance = ModContent.GetInstance<LightingConfig>();
        }

        public override void OnWorldLoad()
        {
            if (Main.netMode == NetmodeID.Server) return;

            SmoothLighting SmoothLightingObj = ModContent.GetInstance<FancyLightingMod>()?.SmoothLightingObj;
            if (SmoothLightingObj is not null) {
                SmoothLightingObj.printExceptionTime = 60;
            }

            base.OnWorldLoad();
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.Server) return;

            UpdateSettings();

            base.PostUpdateEverything();
        }

        internal void UpdateSettings()
        {
            FancyLightingMod._smoothLightingEnabled = _configInstance.UseSmoothLighting && Lighting.UsingNewLighting;
            FancyLightingMod._blurLightMap = _configInstance.UseLightMapBlurring;
            FancyLightingMod._customUpscalingEnabled = _configInstance.UseCustomUpscaling;
            FancyLightingMod._renderOnlyLight = _configInstance.RenderOnlyLight;

            FancyLightingMod._ambientOcclusionEnabled = _configInstance.UseAmbientOcclusion && Lighting.UsingNewLighting;
            FancyLightingMod._ambientOcclusionRadius = _configInstance.AmbientOcclusionRadius;
            FancyLightingMod._ambientOcclusionIntensity = _configInstance.AmbientOcclusionIntensity;

            FancyLightingMod._fancyLightingEngineEnabled = _configInstance.UseFancyLightingEngine;
            FancyLightingMod._fancyLightingEngineUseTemporal = _configInstance.FancyLightingEngineUseTemporal;
            FancyLightingMod._fancyLightingEngineLightLoss = _configInstance.FancyLightingEngineLightLoss;
            FancyLightingMod._fancyLightingEngineMakeBrighter = _configInstance.FancyLightingEngineMakeBrighter;

            FancyLightingMod._threadCount = _configInstance.ThreadCount;
        }
    }
}
