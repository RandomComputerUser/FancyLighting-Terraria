﻿using System.Collections.Generic;
using System.Linq;

namespace FancyLighting.Config;

internal record PresetOptions
{
    public bool UseSmoothLighting { get; init; } = DefaultOptions.UseSmoothLighting;
    public bool UseLightMapBlurring { get; init; } = DefaultOptions.UseLightMapBlurring;
    public bool UseBrighterBlurring { get; init; } = DefaultOptions.UseBrighterBlurring;
    public RenderMode LightMapRenderMode { get; init; } = DefaultOptions.LightMapRenderMode;
    public int NormalMapsStrength { get; init; } = DefaultOptions.NormalMapsStrength;
    public bool QualityNormalMaps { get; init; } = DefaultOptions.QualityNormalMaps;
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
    public bool SimulateGlobalIllumination { get; init; } = DefaultOptions.SimulateGlobalIllumination;
    public bool UseEnhancedFancyLightingEngine { get; init; } = DefaultOptions.UseEnhancedFancyLightingEngine;

    public bool UseCustomSkyColors { get; init; } = DefaultOptions.UseCustomSkyColors;
    public SkyColorPreset CustomSkyPreset { get; init; } = DefaultOptions.CustomSkyPreset;

    public int ThreadCount { get; init; } = DefaultOptions.RuntimeDefaultThreadCount;
    public bool UseHiDefFeatures { get; init; } = DefaultOptions.UseHiDefFeatures;

    public PresetOptions() { }

    public PresetOptions(LightingConfig config)
    {
        UseSmoothLighting = config.UseSmoothLighting;
        UseLightMapBlurring = config.UseLightMapBlurring;
        UseBrighterBlurring = config.UseBrighterBlurring;
        LightMapRenderMode = config.LightMapRenderMode;
        NormalMapsStrength = config.NormalMapsStrength;
        QualityNormalMaps = config.QualityNormalMaps;
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
        SimulateGlobalIllumination = config.SimulateGlobalIllumination;
        UseEnhancedFancyLightingEngine = config.UseEnhancedFancyLightingEngine;

        UseCustomSkyColors = config.UseCustomSkyColors;
        CustomSkyPreset = config.CustomSkyPreset;

        ThreadCount = config.ThreadCount;
        UseHiDefFeatures = config.UseHiDefFeatures;
    }

    public static PresetOptions VanillaPresetOptions = new()
    {
        UseSmoothLighting = false,
        UseBrighterBlurring = false,
        LightMapRenderMode = RenderMode.Bilinear,
        NormalMapsStrength = 0,
        QualityNormalMaps = false,
        UseAmbientOcclusion = false,
        DoNonSolidAmbientOcclusion = false,
        DoTileEntityAmbientOcclusion = false,
        UseFancyLightingEngine = false,
        FancyLightingEngineMakeBrighter = false,
        FancyLightingEngineLightLoss = 50,
        SimulateGlobalIllumination = false,
        UseEnhancedFancyLightingEngine = false,
        UseCustomSkyColors = false,
        UseHiDefFeatures = false,
    };

    public static PresetOptions LowPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseBrighterBlurring = false,
        LightMapRenderMode = RenderMode.Bilinear,
        NormalMapsStrength = 0,
        QualityNormalMaps = false,
        UseAmbientOcclusion = false,
        DoNonSolidAmbientOcclusion = false,
        DoTileEntityAmbientOcclusion = false,
        UseFancyLightingEngine = false,
        FancyLightingEngineMakeBrighter = false,
        FancyLightingEngineLightLoss = 50,
        SimulateGlobalIllumination = false,
        UseEnhancedFancyLightingEngine = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions MediumPresetOptions = new();

    public static PresetOptions HighPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseBrighterBlurring = true,
        LightMapRenderMode = RenderMode.Bicubic,
        NormalMapsStrength = 0,
        QualityNormalMaps = false,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 50,
        SimulateGlobalIllumination = false,
        UseEnhancedFancyLightingEngine = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions VeryHighPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseBrighterBlurring = true,
        LightMapRenderMode = RenderMode.BicubicOverbright,
        NormalMapsStrength = 100,
        QualityNormalMaps = true,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 50,
        SimulateGlobalIllumination = false,
        UseEnhancedFancyLightingEngine = false,
        UseCustomSkyColors = true,
        UseHiDefFeatures = false,
    };

    public static PresetOptions UltraPresetOptions = new()
    {
        UseSmoothLighting = true,
        UseBrighterBlurring = true,
        LightMapRenderMode = RenderMode.BicubicOverbright,
        NormalMapsStrength = 100,
        QualityNormalMaps = true,
        UseAmbientOcclusion = true,
        DoNonSolidAmbientOcclusion = true,
        DoTileEntityAmbientOcclusion = true,
        UseFancyLightingEngine = true,
        FancyLightingEngineMakeBrighter = true,
        FancyLightingEngineLightLoss = 60,
        SimulateGlobalIllumination = true,
        UseEnhancedFancyLightingEngine = true,
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
