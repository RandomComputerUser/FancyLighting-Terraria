using System.ComponentModel;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class LightingConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static LightingConfig Instance;

    internal bool ModifyCameraModeRendering() =>
        SmoothLightingEnabled() || AmbientOcclusionEnabled();

    internal bool SmoothLightingEnabled() =>
        UseSmoothLighting && Lighting.UsingNewLighting;

    internal bool UseBicubicScaling() => LightMapRenderMode is not RenderMode.Bilinear;

    internal bool DrawOverbright() => LightMapRenderMode is RenderMode.BicubicOverbright;

    internal bool AmbientOcclusionEnabled() =>
        UseAmbientOcclusion && Lighting.UsingNewLighting;

    internal bool FancyLightingEngineEnabled() =>
        UseFancyLightingEngine && Lighting.UsingNewLighting;

    internal bool HiDefFeaturesEnabled() =>
        UseHiDefFeatures && Main.graphics.GraphicsProfile is GraphicsProfile.HiDef;

    internal bool DoGammaCorrection() =>
        HiDefFeaturesEnabled() && SmoothLightingEnabled() && DrawOverbright();

    public override void OnChanged() =>
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();

    internal void CopyFrom(PresetOptions options)
    {
        _useHiDefFeatures = options.UseHiDefFeatures;

        _useSmoothLighting = options.UseSmoothLighting;
        _useLightMapBlurring = options.UseLightMapBlurring;
        _useEnhancedBlurring = options.UseEnhancedBlurring;
        _lightMapRenderMode = options.LightMapRenderMode;
        _simulateNormalMaps = options.SimulateNormalMaps;

        _useAmbientOcclusion = options.UseAmbientOcclusion;
        _doNonSolidAmbientOcclusion = options.DoNonSolidAmbientOcclusion;
        _doTileEntityAmbientOcclusion = options.DoTileEntityAmbientOcclusion;

        _useFancyLightingEngine = options.UseFancyLightingEngine;
        _fancyLightingEngineUseTemporal = options.FancyLightingEngineUseTemporal;
        _fancyLightingEngineMakeBrighter = options.FancyLightingEngineMakeBrighter;
        _fancyLightingEngineMode = options.FancyLightingEngineMode;
        _simulateGlobalIllumination = options.SimulateGlobalIllumination;
    }

    public void UpdatePreset()
    {
        var currentOptions = new PresetOptions(this);
        var isPreset = PresetOptions.PresetLookup.TryGetValue(
            currentOptions,
            out var preset
        );
        _preset = isPreset ? preset : Preset.CustomPreset;
    }

    // Serialize this last
    [JsonProperty(Order = 1000)]
    [DefaultValue(DefaultOptions.QualityPreset)]
    [DrawTicks]
    public Preset QualityPreset
    {
        get => _preset;
        set
        {
            if (value is Preset.CustomPreset)
            {
                UpdatePreset();
            }
            else
            {
                var isPresetOptions = PresetOptions.PresetOptionsLookup.TryGetValue(
                    value,
                    out var presetOptions
                );
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

    [DefaultValue(DefaultOptions.UseHiDefFeatures)]
    public bool UseHiDefFeatures
    {
        get => _useHiDefFeatures;
        set
        {
            _useHiDefFeatures = value;
            UpdatePreset();
        }
    }
    private bool _useHiDefFeatures;

    // Smooth Lighting, Normal Maps, Overbright
    [Header("SmoothLighting")]
    [DefaultValue(DefaultOptions.UseSmoothLighting)]
    public bool UseSmoothLighting
    {
        get => _useSmoothLighting;
        set
        {
            _useSmoothLighting = value;
            UpdatePreset();
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
            UpdatePreset();
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
            UpdatePreset();
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
            UpdatePreset();
        }
    }
    private RenderMode _lightMapRenderMode;

    [DefaultValue(DefaultOptions.SimulateNormalMaps)]
    public bool SimulateNormalMaps
    {
        get => _simulateNormalMaps;
        set
        {
            _simulateNormalMaps = value;
            UpdatePreset();
        }
    }
    private bool _simulateNormalMaps;

    // Ambient Occlusion
    [Header("AmbientOcclusion")]
    [DefaultValue(DefaultOptions.UseAmbientOcclusion)]
    public bool UseAmbientOcclusion
    {
        get => _useAmbientOcclusion;
        set
        {
            _useAmbientOcclusion = value;
            UpdatePreset();
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
            UpdatePreset();
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
            UpdatePreset();
        }
    }
    private bool _doTileEntityAmbientOcclusion;

    // Fancy Lighting Engine
    [Header("LightingEngine")]
    [DefaultValue(DefaultOptions.UseFancyLightingEngine)]
    public bool UseFancyLightingEngine
    {
        get => _useFancyLightingEngine;
        set
        {
            _useFancyLightingEngine = value;
            UpdatePreset();
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
            UpdatePreset();
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
            UpdatePreset();
        }
    }
    private bool _fancyLightingEngineMakeBrighter;

    [DefaultValue(DefaultOptions.FancyLightingEngineMode)]
    [DrawTicks]
    public LightingEngineMode FancyLightingEngineMode
    {
        get => _fancyLightingEngineMode;
        set
        {
            _fancyLightingEngineMode = value;
            UpdatePreset();
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
            UpdatePreset();
        }
    }
    private bool _simulateGlobalIllumination;
}
