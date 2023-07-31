using System.Collections.Generic;
using System.Linq;

namespace FancyLighting.Config;

internal record PresetOptions
{
    public bool UseSmoothLighting { get; init; } = DefaultOptions.UseSmoothLighting;
    public bool UseLightMapBlurring { get; init; } = DefaultOptions.UseLightMapBlurring;
    public bool UseEnhancedBlurring { get; init; } = DefaultOptions.UseEnhancedBlurring;
    public RenderMode LightMapRenderMode { get; init; } = DefaultOptions.LightMapRenderMode;
    public int NormalMapsStrength { get; init; } = DefaultOptions.NormalMapsStrength;
    public bool FineNormalMaps { get; init; } = DefaultOptions.FineNormalMaps;
    public bool RenderOnlyLight { get; init; } = DefaultOptions.RenderOnlyLight;

    public bool UseAmbientOcclusion { get; init; } = DefaultOptions.UseAmbientOcclusion;
    public bool DoNonSolidAmbientOcclusion { get; init; } = DefaultOptions.DoNonSolidAmbientOcclusion;
    public bool DoTileEntityAmbientOcclusion { get; init; } = DefaultOptions.DoTileEntityAmbientOcclusion;
    public int AmbientOcclusionRadius { get; init; } = DefaultOptions.AmbientOcclusionRadius;
    public int AmbientOcclusionIntensity { get; init; } = DefaultOptions.AmbientOcclusionIntensity;

    public bool UseFancyLightingEngine { get; init; } = DefaultOptions.UseFancyLightingEngine;
    public bool FancyLightingEngineUseTemporal { get; init; } = DefaultOptions.FancyLightingEngineUseTemporal;
    public bool FancyLightingEngineMakeBrighter { get; init; } = DefaultOptions.FancyLightingEngineMakeBrighter;
    public int FancyLightingEngineLightLoss { get; init; } = DefaultOptions.FancyLightingEngineLightLoss;
    public int FancyLightingEngineLightAbsorption { get; init; } = DefaultOptions.FancyLightingEngineLightAbsorption;
    public LightingEngineMode FancyLightingEngineMode { get; init; } = DefaultOptions.FancyLightingEngineMode;
    public bool SimulateGlobalIllumination { get; init; } = DefaultOptions.SimulateGlobalIllumination;

    public bool UseCustomSkyColors { get; init; } = DefaultOptions.UseCustomSkyColors;
    public SkyColorPreset CustomSkyPreset { get; init; } = DefaultOptions.CustomSkyPreset;

    public int ThreadCount { get; init; } = DefaultOptions.RuntimeDefaultThreadCount;
    public bool UseHiDefFeatures { get; init; } = DefaultOptions.UseHiDefFeatures;

    public PresetOptions() { }

    public PresetOptions(LightingConfig config)
    {
        UseSmoothLighting = config.UseSmoothLighting;
        UseLightMapBlurring = config.UseLightMapBlurring;
        UseEnhancedBlurring = config.UseEnhancedBlurring;
        LightMapRenderMode = config.LightMapRenderMode;
        NormalMapsStrength = config.NormalMapsStrength;
        FineNormalMaps = config.FineNormalMaps;
        RenderOnlyLight = config.RenderOnlyLight;

        UseAmbientOcclusion = config.UseAmbientOcclusion;
        DoNonSolidAmbientOcclusion = config.DoNonSolidAmbientOcclusion;
        DoTileEntityAmbientOcclusion = config.DoTileEntityAmbientOcclusion;
        AmbientOcclusionRadius = config.AmbientOcclusionRadius;
        AmbientOcclusionIntensity = config.AmbientOcclusionIntensity;

        UseFancyLightingEngine = config.UseFancyLightingEngine;
        FancyLightingEngineUseTemporal = config.FancyLightingEngineUseTemporal;
        FancyLightingEngineMakeBrighter = config.FancyLightingEngineMakeBrighter;
        FancyLightingEngineLightLoss = config.FancyLightingEngineLightLoss;
        FancyLightingEngineLightAbsorption = config.FancyLightingEngineLightAbsorption;
        FancyLightingEngineMode = config.FancyLightingEngineMode;
        SimulateGlobalIllumination = config.SimulateGlobalIllumination;

        UseCustomSkyColors = config.UseCustomSkyColors;
        CustomSkyPreset = config.CustomSkyPreset;

        ThreadCount = config.ThreadCount;
        UseHiDefFeatures = config.UseHiDefFeatures;
    }

    public static PresetOptions VanillaPresetOptions = new()
    {
        UseSmoothLighting = false,
        UseEnhancedBlurring = false,
        LightMapRenderMode = RenderMode.Bilinear,
        NormalMapsStrength = 0,
        UseAmbientOcclusion = false,
        DoNonSolidAmbientOcclusion = false,
        DoTileEntityAmbientOcclusion = false,
        UseFancyLightingEngine = false,
        FancyLightingEngineMakeBrighter = false,
        FancyLightingEngineLightLoss = 50,
        FancyLightingEngineMode = LightingEngineMode.One,
        SimulateGlobalIllumination = false,
        UseCustomSkyColors = false,
        UseHiDefFeatures = false,
    };

    public static PresetOptions LowPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseEnhancedBlurring = false,
        LightMapRenderMode = RenderMode.Bilinear,
        NormalMapsStrength = 0,
        UseAmbientOcclusion = false,
        DoNonSolidAmbientOcclusion = false,
        DoTileEntityAmbientOcclusion = false,
        UseFancyLightingEngine = false,
        FancyLightingEngineMakeBrighter = false,
        FancyLightingEngineLightLoss = 50,
        FancyLightingEngineMode = LightingEngineMode.One,
        SimulateGlobalIllumination = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions MediumPresetOptions = new();

    public static PresetOptions HighPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseEnhancedBlurring = true,
        LightMapRenderMode = RenderMode.Bicubic,
        NormalMapsStrength = 0,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 50,
        FancyLightingEngineMode = LightingEngineMode.One,
        SimulateGlobalIllumination = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions VeryHighPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseEnhancedBlurring = true,
        LightMapRenderMode = RenderMode.BicubicOverbright,
        NormalMapsStrength = 100,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 60,
        FancyLightingEngineMode = LightingEngineMode.Two,
        SimulateGlobalIllumination = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions UltraPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseEnhancedBlurring = true,
        LightMapRenderMode = RenderMode.BicubicOverbright,
        NormalMapsStrength = 100,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 60,
        FancyLightingEngineMode = LightingEngineMode.Four,
        SimulateGlobalIllumination = true,
        UseCustomSkyColors = true,
        UseHiDefFeatures = true,
    };

    public static Dictionary<PresetOptions, Preset> PresetLookup = new()
    {
        [VanillaPresetOptions] = Preset.VanillaPreset,
        [LowPresetOptions] = Preset.LowPreset,
        [MediumPresetOptions] = Preset.MediumPreset,
        [HighPresetOptions] = Preset.HighPreset,
        [VeryHighPresetOptions] = Preset.VeryHighPreset,
        [UltraPresetOptions] = Preset.UltraPreset,
    };

    public static Dictionary<Preset, PresetOptions> PresetOptionsLookup =
        PresetLookup.ToDictionary(entry => entry.Value, entry => entry.Key);
}
