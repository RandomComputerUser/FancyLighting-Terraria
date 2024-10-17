using System;

namespace FancyLighting.Config;

public static class DefaultOptions
{
    public const Preset QualityPreset = Preset.MediumPreset;

    public const int ThreadCount = -1; // Used for the DefaultValue attribute in PreferencesConfig
    public const int MinThreadCount = 1;
    public const int MaxThreadCount = 32;
    public const int MaxDefaultThreadCount = 16;
    public static int RuntimeDefaultThreadCount =>
        Math.Clamp(Environment.ProcessorCount, MinThreadCount, MaxDefaultThreadCount);
    public const bool UseHiDefFeatures = false;

    public const bool UseSmoothLighting = true;
    public const bool UseLightMapBlurring = true;
    public const bool UseEnhancedBlurring = false;
    public const bool UseLightMapToneMapping = true;
    public const RenderMode LightMapRenderMode = RenderMode.Bilinear;
    public const bool SimulateNormalMaps = false;
    public const int NormalMapsStrength = 100;
    public const bool FineNormalMaps = false;
    public const bool RenderOnlyLight = false;

    public const bool UseAmbientOcclusion = true;
    public const bool DoNonSolidAmbientOcclusion = true;
    public const bool DoTileEntityAmbientOcclusion = false;
    public const int AmbientOcclusionRadius = 2;
    public const int AmbientOcclusionIntensity = 90;
    public const int AmbientLightProportion = 40;

    public const bool UseFancyLightingEngine = true;
    public const bool FancyLightingEngineUseTemporal = true;
    public const bool FancyLightingEngineMakeBrighter = false;
    public const int FancyLightingEngineLightLoss = 50;
    public const int FancyLightingEngineLightAbsorption = 100;
    public const LightingEngineMode FancyLightingEngineMode = LightingEngineMode.One;
    public const bool SimulateGlobalIllumination = false;

    public const bool UseCustomSkyColors = true;
    public const SkyColorPreset CustomSkyPreset = SkyColorPreset.Profile1;
}
