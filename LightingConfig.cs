using Terraria.ModLoader.Config;

using System.ComponentModel;

namespace FancyLighting
{
    class LightingConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[DefaultValue(true)]
		[Label("Use Smooth Lighting")]
		public bool UseSmoothLighting;

		[DefaultValue(true)]
		[Label("Use Ambient Occlusion")]
		public bool UseAmbientOcclusion;
	}
}