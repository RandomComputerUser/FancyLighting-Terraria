using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class LightingConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static LightingConfig Instance;

    internal bool ModifyCameraModeRendering() => SmoothLightingEnabled() || AmbientOcclusionEnabled();
    internal bool SmoothLightingEnabled() => UseSmoothLighting && Lighting.UsingNewLighting;
    internal bool UseBicubicScaling() => LightMapRenderMode != RenderMode.Bilinear;
    internal bool DrawOverbright() => LightMapRenderMode == RenderMode.BicubicOverbright;
    internal bool UseNormalMaps() => NormalMapsStrength != 0;
    internal float NormalMapsMultiplier() => NormalMapsStrength / 100f;
    internal bool AmbientOcclusionEnabled() => UseAmbientOcclusion && Lighting.UsingNewLighting;
    internal float AmbientOcclusionAlpha() => 1f - AmbientOcclusionIntensity / 100f;
    internal bool FancyLightingEngineEnabled() => UseFancyLightingEngine && Lighting.UsingNewLighting;
    internal float FancyLightingEngineExitMultiplier() => 1f - FancyLightingEngineLightLoss / 100f;
    internal float FancyLightingEngineAbsorptionExponent() => FancyLightingEngineLightAbsorption / 100f;
    internal bool CustomSkyColorsEnabled() => UseCustomSkyColors && Lighting.UsingNewLighting;
    internal bool HiDefFeaturesEnabled()
        => UseHiDefFeatures && Main.instance.GraphicsDevice.GraphicsProfile == GraphicsProfile.HiDef;
    internal bool DoGammaCorrection()
        => HiDefFeaturesEnabled() && SmoothLightingEnabled() && DrawOverbright();

    public override void OnChanged()
        => ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();

    private void CopyFrom(PresetOptions options)
    {
        _useSmoothLighting = options.UseSmoothLighting;
        _useLightMapBlurring = options.UseLightMapBlurring;
        _useEnhancedBlurring = options.UseEnhancedBlurring;
        _lightMapRenderMode = options.LightMapRenderMode;
        _normalMapsStrength = options.NormalMapsStrength;
        _useQualityNormalMaps = options.QualityNormalMaps;
        _useFineNormalMaps = options.FineNormalMaps;
        _renderOnlyLight = options.RenderOnlyLight;

        _useAmbientOcclusion = options.UseAmbientOcclusion;
        _doNonSolidAmbientOcclusion = options.DoNonSolidAmbientOcclusion;
        _doTileEntityAmbientOcclusion = options.DoTileEntityAmbientOcclusion;
        _ambientOcclusionRadius = options.AmbientOcclusionRadius;
        _ambientOcclusionIntensity = options.AmbientOcclusionIntensity;

        _useFancyLightingEngine = options.UseFancyLightingEngine;
        _fancyLightingEngineUseTemporal = options.FancyLightingEngineUseTemporal;
        _fancyLightingEngineMakeBrighter = options.FancyLightingEngineMakeBrighter;
        _fancyLightingEngineLightLoss = options.FancyLightingEngineLightLoss;
        _fancyLightingEngineLightAbsorption = options.FancyLightingEngineLightAbsorption;
        _fancyLightingEngineMode = options.FancyLightingEngineMode;
        _simulateGlobalIllumination = options.SimulateGlobalIllumination;

        _useCustomSkyColors = options.UseCustomSkyColors;
        _customSkyPreset = options.CustomSkyPreset;

        _threadCount = options.ThreadCount;
        _useHiDefFeatures = options.UseHiDefFeatures;
    }

    // Presets
    [Header("Presets")]

    // Serialize this last
    [JsonProperty(Order = 1000)]
    [DefaultValue(DefaultOptions.ConfigPreset)]
    [DrawTicks]
    public Preset ConfigPreset
    {
        get => _preset;
        set
        {
            if (value == Preset.CustomPreset)
            {
                PresetOptions currentOptions = new(this);
                bool isPreset
                    = PresetOptions.PresetLookup.TryGetValue(currentOptions, out Preset preset);
                if (isPreset)
                {
                    _preset = preset;
                }
                else
                {
                    _preset = Preset.CustomPreset;
                }
            }
            else
            {
                bool isPresetOptions
                    = PresetOptions.PresetOptionsLookup.TryGetValue(value, out PresetOptions presetOptions);
                if (isPresetOptions)
                {
                    CopyFrom(presetOptions);
                    _preset = value;
                }
                else
                {
                    _preset = Preset.CustomPreset;
                }
            }
        }
    }
    private Preset _preset;

    // Smooth Lighting, Normal Maps, Overbright
    [Header("SmoothLighting")]

    [DefaultValue(DefaultOptions.UseSmoothLighting)]
    public bool UseSmoothLighting
    {
        get => _useSmoothLighting;
        set
        {
            _useSmoothLighting = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useSmoothLighting;

    [DefaultValue(DefaultOptions.UseLightMapBlurring)]
    public bool UseLightMapBlurring
    {
        get => _useLightMapBlurring;
        set
        {
            _useLightMapBlurring = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useLightMapBlurring;

    [DefaultValue(DefaultOptions.UseEnhancedBlurring)]
    public bool UseEnhancedBlurring
    {
        get => _useEnhancedBlurring;
        set
        {
            _useEnhancedBlurring = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useEnhancedBlurring;

    [DefaultValue(DefaultOptions.LightMapRenderMode)]
    [DrawTicks]
    public RenderMode LightMapRenderMode
    {
        get => _lightMapRenderMode;
        set
        {
            _lightMapRenderMode = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private RenderMode _lightMapRenderMode;

    [Range(0, 200)]
    [Increment(25)]
    [DefaultValue(DefaultOptions.NormalMapsStrength)]
    [Slider]
    [DrawTicks]
    public int NormalMapsStrength
    {
        get => _normalMapsStrength;
        set
        {
            _normalMapsStrength = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _normalMapsStrength;

    [DefaultValue(DefaultOptions.QualityNormalMaps)]
    public bool QualityNormalMaps
    {
        get => _useQualityNormalMaps;
        set
        {
            _useQualityNormalMaps = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useQualityNormalMaps;

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps
    {
        get => _useFineNormalMaps;
        set
        {
            _useFineNormalMaps = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useFineNormalMaps;

    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight
    {
        get => _renderOnlyLight;
        set
        {
            _renderOnlyLight = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _renderOnlyLight;

    // Ambient Occlusion
    [Header("AmbientOcclusion")]

    [DefaultValue(DefaultOptions.UseAmbientOcclusion)]
    public bool UseAmbientOcclusion
    {
        get => _useAmbientOcclusion;
        set
        {
            _useAmbientOcclusion = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useAmbientOcclusion;

    [DefaultValue(DefaultOptions.DoNonSolidAmbientOcclusion)]
    public bool DoNonSolidAmbientOcclusion
    {
        get => _doNonSolidAmbientOcclusion;
        set
        {
            _doNonSolidAmbientOcclusion = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _doNonSolidAmbientOcclusion;

    [DefaultValue(DefaultOptions.DoTileEntityAmbientOcclusion)]
    public bool DoTileEntityAmbientOcclusion
    {
        get => _doTileEntityAmbientOcclusion;
        set
        {
            _doTileEntityAmbientOcclusion = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _doTileEntityAmbientOcclusion;

    [Range(1, 6)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.AmbientOcclusionRadius)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionRadius
    {
        get => _ambientOcclusionRadius;
        set
        {
            _ambientOcclusionRadius = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _ambientOcclusionRadius;

    [Range(5, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.AmbientOcclusionIntensity)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionIntensity
    {
        get => _ambientOcclusionIntensity;
        set
        {
            _ambientOcclusionIntensity = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _ambientOcclusionIntensity;

    // Fancy Lighting Engine
    [Header("LightingEngine")]

    [DefaultValue(DefaultOptions.UseFancyLightingEngine)]
    public bool UseFancyLightingEngine
    {
        get => _useFancyLightingEngine;
        set
        {
            _useFancyLightingEngine = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useFancyLightingEngine;

    [DefaultValue(DefaultOptions.FancyLightingEngineUseTemporal)]
    public bool FancyLightingEngineUseTemporal
    {
        get => _fancyLightingEngineUseTemporal;
        set
        {
            _fancyLightingEngineUseTemporal = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _fancyLightingEngineUseTemporal;

    [DefaultValue(DefaultOptions.FancyLightingEngineMakeBrighter)]
    public bool FancyLightingEngineMakeBrighter
    {
        get => _fancyLightingEngineMakeBrighter;
        set
        {
            _fancyLightingEngineMakeBrighter = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _fancyLightingEngineMakeBrighter;

    [Range(0, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightLoss)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineLightLoss
    {
        get => _fancyLightingEngineLightLoss;
        set
        {
            _fancyLightingEngineLightLoss = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _fancyLightingEngineLightLoss;

    [Range(70, 200)]
    [Increment(10)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightAbsorption)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineLightAbsorption
    {
        get => _fancyLightingEngineLightAbsorption;
        set
        {
            _fancyLightingEngineLightAbsorption = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _fancyLightingEngineLightAbsorption;

    [DefaultValue(DefaultOptions.FancyLightingEngineMode)]
    [DrawTicks]
    public LightingEngineMode FancyLightingEngineMode
    {
        get => _fancyLightingEngineMode;
        set
        {
            _fancyLightingEngineMode = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private LightingEngineMode _fancyLightingEngineMode;

    [DefaultValue(DefaultOptions.SimulateGlobalIllumination)]
    public bool SimulateGlobalIllumination
    {
        get => _simulateGlobalIllumination;
        set
        {
            _simulateGlobalIllumination = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _simulateGlobalIllumination;

    // Sky Color
    [Header("SkyColor")]

    [DefaultValue(DefaultOptions.UseCustomSkyColors)]
    public bool UseCustomSkyColors
    {
        get => _useCustomSkyColors;
        set
        {
            _useCustomSkyColors = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useCustomSkyColors;

    [DefaultValue(DefaultOptions.CustomSkyPreset)]
    [DrawTicks]
    public SkyColorPreset CustomSkyPreset
    {
        get => _customSkyPreset;
        set
        {
            _customSkyPreset = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private SkyColorPreset _customSkyPreset;

    // Other Settings
    [Header("General")]

    [Range(DefaultOptions.MinThreadCount, DefaultOptions.MaxThreadCount)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.ThreadCount)]
    public int ThreadCount
    {
        get => _threadCount;
        set
        {
            _threadCount = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private int _threadCount;

    [DefaultValue(DefaultOptions.UseHiDefFeatures)]
    public bool UseHiDefFeatures
    {
        get => _useHiDefFeatures;
        set
        {
            _useHiDefFeatures = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useHiDefFeatures;
}
