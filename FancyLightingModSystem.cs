using Terraria;
using Terraria.ModLoader;

namespace FancyLighting
{
    class FancyLightingModSystem : ModSystem
    {

        public override void PostUpdateEverything()
        {
            UpdateSettings();

            base.PostUpdateEverything();
        }

        internal static void UpdateSettings()
        {
            FancyLightingMod._smoothLightingEnabled = ModContent.GetInstance<LightingConfig>().UseSmoothLighting && Lighting.UsingNewLighting;
            FancyLightingMod._ambientOcclusionEnabled = ModContent.GetInstance<LightingConfig>().UseAmbientOcclusion && Lighting.UsingNewLighting;
            FancyLightingMod._ambientOcclusionRadius = ModContent.GetInstance<LightingConfig>().AmbientOcclusionRadius;
            FancyLightingMod._ambientOcclusionIntensity = ModContent.GetInstance<LightingConfig>().AmbientOcclusionIntensity;
        }

    }
}
