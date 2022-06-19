using Terraria.ModLoader;

using System;

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
			FancyLightingMod._smoothLightingEnabled = ModContent.GetInstance<LightingConfig>().UseSmoothLighting;
			FancyLightingMod._ambientOcclusionEnabled = ModContent.GetInstance<LightingConfig>().UseAmbientOcclusion;
		}

	}
}
