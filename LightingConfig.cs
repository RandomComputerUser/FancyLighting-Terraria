using Terraria.ModLoader.Config;

using System.ComponentModel;

namespace FancyLighting
{
    [Label("Fancy Lighting Settings")]
    public class LightingConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [Header("Smooth Lighting")]
        [DefaultValue(true)]
        [Label("Enable Smooth Lighting")]
        [Tooltip("Toggles whether or not to use smooth lighting\nIf disabled, vanilla lighting visuals are used\nRequires lighting to be set to color")]
        public bool UseSmoothLighting;

        [DefaultValue(true)]
        [Label("Blur Light Map")]
        [Tooltip("Toggles whether or not to blur the light map\nApplies a per-tile blur to the light map before rendering\nSmooths jagged corners in the light map\nDisabling this setting may slightly increase performance")]
        public bool UseLightMapBlurring;

        [DefaultValue(false)]
        [Label("High-Quality Upscaling")]
        [Tooltip("Toggles whether or not to use a custom bicubic shader to upscale the light map texture\nMakes lighting transitions between tiles slightly smoother\nIf disabled, the default scaling (bilinear) is applied\nEnabling this setting may degrade performance")]
        public bool UseCustomUpscaling;

        [DefaultValue(false)]
        [Label("(Debug) Render Only Lighting")]
        [Tooltip("When enabled, tiles, walls, and the background aren't rendered")]
        public bool RenderOnlyLight;

        [Header("Ambient Occlusion")]
        [DefaultValue(true)]
        [Label("Enable Ambient Occlusion")]
        [Tooltip("Toggles whether or not to use ambient occlusion\nIf enabled, shadows are added around the edges of foreground tiles in front of background walls\nRequires lighting to be set to color")]
        public bool UseAmbientOcclusion;

        [Range(1, 7)]
        [Increment(1)]
        [DefaultValue(5)]
        [Slider]
        [DrawTicks]
        [Label("Ambient Occlusion Radius")]
        [Tooltip("Controls the radius of blur used in ambient occlusion\nHigher values correspond to a larger blur radius\nHigher values may degrade performance")]
        public int AmbientOcclusionRadius;

        [Range(5, 100)]
        [Increment(5)]
        [DefaultValue(35)]
        [Slider]
        [DrawTicks]
        [Label("Ambient Occlusion Intensity")]
        [Tooltip("Controls the intensity of shadows in ambient occlusion\nHigher values correspond to darker ambient occlusion shadows")]
        public int AmbientOcclusionIntensity;

        [Header("Lighting Engine")]
        [DefaultValue(true)]
        [Label("Enable Fancy Lighting Engine")]
        [Tooltip("Toggles whether or not to use a modified lighting engine\nWhen enabled, light is spread more accurately\nShadows should face away from light sources and be more noticeable\nPerformance is significantly reduced in areas with more light sources\nRequires lighting to be set to color")]
        public bool UseFancyLightingEngine;

        [DefaultValue(true)]
        [Label("Temporal Optimization")]
        [Tooltip("Toggles whether or not to use temporal optimization with the fancy lighting engine\nWhen enabled, data from the previous update is used to optimize lighting during the current update\nMakes lighting quicker in more intensly lit areas\nMay sometimes result in a slightly lowered lighting quality")]
        public bool FancyLightingEngineUseTemporal;

        [DefaultValue(false)]
        [Label("Brighter Lighting")]
        [Tooltip("Toggles whether or not to make lighting slightly brighter\nWhen disabled, lighting is slightly darker than when using the vanilla lighting engine\nSlightly degrades performance when enabled")]
        public bool FancyLightingEngineMakeBrighter;

        [Range(0, 65)]
        [Increment(5)]
        [DefaultValue(50)]
        [Slider]
        [DrawTicks]
        [Label("Light Loss (%) When Exiting Solid Blocks")]
        [Tooltip("Controls how much light is lost when light exits a solid block into the air\nHigher values correspond to darker shadows")]
        public int FancyLightingEngineLightLoss;

        [Header("General")]
        [Range(1, 24)]
        [Increment(1)]
        [DefaultValue(8)]
        [Label("Thread Count")]
        [Tooltip("Controls how many threads smooth lighting and the fancy lighting engine use\nFor good results, set this to the number of threads your CPU has")]
        public int ThreadCount;
    }
}