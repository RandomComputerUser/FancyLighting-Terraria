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

        [Header("Fancy Lighting Engine (Experimental)")]
        [DefaultValue(false)]
        [ReloadRequired]
        [Label("Enable Fancy Lighting Engine (Reload Required)")]
        [Tooltip("Toggles whether or not to use a modified lighting engine.\nIf turned on, light will travel in straight lines in all directions from a light source.\nShadows should face nearly directly away from light sources.\nRequires lighting to be set to color.\nSeverely degrades performance; use only with a fast computer.")]
        public bool UseFancyLightingEngine;

        [Range(1, 24)]
        [Increment(1)]
        [DefaultValue(8)]
        [Label("Fancy Lighting Engine Thread Count")]
        [Tooltip("Controls how many threads the fancy lighting engine uses.\nFor good results, set this to the number of threads your CPU has.\n")]
        public int FancyLightingEngineThreadCount;
    }

}