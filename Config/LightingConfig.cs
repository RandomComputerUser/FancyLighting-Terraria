using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

[Label("Fancy Lighting Settings")]
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
    internal bool UseGammaCorrection()
        => HiDefFeaturesEnabled() && SmoothLightingEnabled() && DrawOverbright();
    internal bool DoRayTracing() => HiDefFeaturesEnabled() && UseRayTracing;

    public override void OnChanged()
        => ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();

    private void CopyFrom(PresetOptions options)
    {
        _useSmoothLighting = options.UseSmoothLighting;
        _useLightMapBlurring = options.UseLightMapBlurring;
        _useBrighterBlurring = options.UseBrighterBlurring;
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
        _simulateGlobalIllumination = options.SimulateGlobalIllumination;
        _useRayTracing = options.UseRayTracing;

        _useCustomSkyColors = options.UseCustomSkyColors;
        _customSkyPreset = options.CustomSkyPreset;

        _threadCount = options.ThreadCount;
        _useHiDefFeatures = options.UseHiDefFeatures;
    }

    // Presets
    [Header("Presets")]

    // Serialize this last
    [JsonProperty(Order = 1000)]
    [Label("Settings Preset")]
    [Tooltip("A preset for the settings below may be chosen\nLower presets have better performance but lower quality")]
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
    [Header("Smooth Lighting")]

    [Label("Enable Smooth Lighting")]
    [Tooltip("Toggles whether to use smooth lighting\nIf disabled, vanilla lighting visuals are used\nRequires lighting to be set to color")]
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

    [Label("Blur Light Map")]
    [Tooltip("Toggles whether to blur the light map\nApplies a per-tile blur to the light map before rendering\nSmooths sharp light transitions\nDisabling this setting may slightly increase performance")]
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

    [Label("Use Brighter Blurring")]
    [Tooltip("Controls the blurring function used to blur the light map\nWhen enabled, light map blurring cannot darken a tile's lighting\nIncreases the brightness of highlights")]
    [DefaultValue(DefaultOptions.UseBrighterBlurring)]
    public bool UseBrighterBlurring
    {
        get => _useBrighterBlurring;
        set
        {
            _useBrighterBlurring = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useBrighterBlurring;

    [Label("Light Map Render Mode")]
    [Tooltip("Controls how the light map is rendered\nAffects the smoothness of lighting\nBicubic upscaling is smoother than bilinear upscaling\nOverbright rendering increases the maximum brightness of light")]
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

    [Label("Simulated Normal Maps Strength")]
    [Tooltip("Controls the strength of simulated normal maps\nWhen not 0, tiles have simulated normal maps and appear bumpy\nSet to 0 to disable")]
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

    [Label("Use Higher-Quality Normal Maps")]
    [Tooltip("Toggles between regular and higher-quality simulated normal map shaders\nWhen enabled, uses a higher-quality normal map simulation\nMay reduce performance when enabled")]
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

    [Label("Use Fine Normal Maps")]
    [Tooltip("Toggles between coarse and fine simulated normal maps\nCoarse normal maps have 2x2 resolution, and fine 1x1\nRecommended to enable when using HD textures")]
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

    [Label("(Debug) Render Only Lighting")]
    [Tooltip("When enabled, tile, wall, and background textures aren't rendered")]
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
    [Header("Ambient Occlusion")]

    [Label("Enable Ambient Occlusion")]
    [Tooltip("Toggles whether to use ambient occlusion\nIf enabled, tiles produce shadows in front of walls\nRequires lighting to be set to color")]
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

    [Label("Enable Ambient Occlusion from Non-Solid Tiles")]
    [Tooltip("Toggles whether non-solid blocks generate ambient occlusion\nNon-solid tiles generate weaker ambient occlusion\nPrimarily affects furniture and torches\nNot all non-solid tiles are affected")]
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

    [Label("Enable Ambient Occlusion from Tile Entities")]
    [Tooltip("Toggles whether tile entities generate ambient occlusion\nTile entities generate weaker ambient occlusion\nPrimarily affects moving, non-solid tiles (e.g., tiles affected by wind)")]
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

    [Label("Ambient Occlusion Radius")]
    [Tooltip("Controls the radius of blur used in ambient occlusion\nHigher values correspond to a larger blur radius\nHigher values may reduce performance")]
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

    [Label("Ambient Occlusion Intensity")]
    [Tooltip("Controls the intensity of shadows in ambient occlusion\nHigher values correspond to darker ambient occlusion shadows")]
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
    [Header("Lighting Engine")]

    [Label("Enable Fancy Lighting Engine")]
    [Tooltip("Toggles whether to use a modified lighting engine\nWhen enabled, light is spread more accurately\nShadows should face away from light sources and be more noticeable\nPerformance is significantly reduced in areas with more light sources\nRequires lighting to be set to color")]
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

    [Label("Temporal Optimization")]
    [Tooltip("Toggles whether to use temporal optimization with the fancy lighting engine\nWhen enabled, uses data from the previous update to optimize lighting calculations\nMakes lighting quicker in more intensly lit areas\nMay sometimes cause lighting quality to be slightly reduced")]
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

    [Label("Brighter Lighting")]
    [Tooltip("Toggles whether to make lighting slightly brighter\nWhen disabled, lighting is slightly darker than with vanilla lighting\nMay reduce performance when enabled")]
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

    [Label("Light Loss (%) Exiting Solid Blocks")]
    [Tooltip("Controls how much light is lost exiting a solid block into the air\nHigher values correspond to darker shadows")]
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

    [Label("Light Absorption (relative %) of Solid Blocks")]
    [Tooltip("Controls how much light is absorbed inside solid blocks\nLower values allow light to spread farther into solid blocks\nA value of 100% is equivalent to vanilla")]
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

    [Label("Simulate Global Illumination")]
    [Tooltip("Toggles whether to simulate a basic form of global illumination\nWhen enabled, indirect lighting makes shadows brighter")]
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

    [Label("(Experimental) Use Ray Tracing")]
    [Tooltip("Toggles whether to use a basic form of ray tracing\nMakes shadows more accurate\nRequires Use Enhanced Shaders and Colors to be enabled\nHas performance issues on slower GPUs\nDoes not currently support simulated global illumination")]
    [DefaultValue(DefaultOptions.UseRayTracing)]
    public bool UseRayTracing
    {
        get => _useRayTracing;
        set
        {
            _useRayTracing = value;
            ConfigPreset = Preset.CustomPreset;
        }
    }
    private bool _useRayTracing;

    // Sky Color
    [Header("Sky Color")]

    [Label("Enable Fancy Sky Colors")]
    [Tooltip("Toggles whether to use modified sky colors\nIf disabled, vanilla sky colors are used instead")]
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

    [Label("Sky Color Profile")]
    [Tooltip("Controls which set of sky colors is used")]
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

    [Label("Thread Count")]
    [Tooltip("Controls how many threads smooth lighting and the fancy lighting engine use\nThe default value should result in the best performance")]
    [Range(1, 24)]
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

    [Label("Use Enhanced Shaders and Colors")]
    [Tooltip("Toggles whether to use enhanced shaders and colors allowed by the HiDef profile\nWhen enabled, some visual effects are improved\nMay significantly decrease rendering performance if enabled")]
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
