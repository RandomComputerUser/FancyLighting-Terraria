using Terraria.ModLoader.Config;

using System.ComponentModel;

namespace FancyLighting
{

    [Label("Fancy Lighting Settings")]
    class LightingConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("Smooth Lighting")]
        [DefaultValue(true)]
        [Label("Enable Smooth Lighting")]
        [Tooltip("Toggles whether or not to use smooth lighting.\nIf turned off, vanilla lighting visuals are used.\nRequires lighting to be set to color.")]
        public bool UseSmoothLighting;

        [Header("Ambient Occlusion")]
        [DefaultValue(true)]
        [Label("Enable Ambient Occlusion")]
        [Tooltip("Toggles whether or not to use ambient occlusion.\nIf turned on, shadows are added around the edges of foreground tiles in front of background walls.\nRequires lighting to be set to color.")]
        public bool UseAmbientOcclusion;

        [Range(1, 7)]
        [Increment(1)]
        [DefaultValue(5)]
        [Slider]
        [DrawTicks]
        [Label("Ambient Occlusion Radius")]
        [Tooltip("Controls the radius of blur used in ambient occlusion.\nHigher values correspond to a larger blur radius.\nHigher values may slightly degrade performance.")]
        public int AmbientOcclusionRadius;

        [Range(5, 100)]
        [Increment(5)]
        [DefaultValue(35)]
        [Slider]
        [DrawTicks]
        [Label("Ambient Occlusion Intensity")]
        [Tooltip("Controls the intensity of shadows in ambient occlusion.\nHigher values correspond to darker shadows.")]
        public int AmbientOcclusionIntensity;
    }

}