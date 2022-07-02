using Terraria;
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

        public override void PostUpdateEverything()
        {
            UpdatePerFrameInfo();

            base.PostUpdateEverything();
        }

        internal void UpdatePerFrameInfo()
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

            FancyLightingMod._threadCount = _configInstance.ThreadCount;
        }
    }
}
