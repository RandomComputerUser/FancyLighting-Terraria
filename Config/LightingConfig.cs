using System;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

[Label("Fancy Lighting Settings")]
public sealed class LightingConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;



    // Presets
    [Header("Presets")]

    [DrawTicks]
    [DefaultValue(Preset.DefaultPreset)]
    [Label("Settings Preset")]
    [Tooltip("A preset for the above settings may be chosen")]
    public Preset ConfigPreset
    {
        get => _preset;
        set
        {
            _preset = value;

            // Bad code ahead

            if (_preset == Preset.DefaultPreset)
            {
                _useSmoothLighting = true;
                _useLightMapBlurring = true;
                _lightMapRenderMode = RenderMode.Bilinear;
                _normalMapsStrength = 0;
                _useQualityNormalMaps = false;
                _useFineNormalMaps = false;
                _renderOnlyLight = false;

                _useAmbientOcclusion = true;
                _doNonSolidAmbientOcclusion = true;
                _doTileEntityAmbientOcclusion = false;
                _ambientOcclusionRadius = 4;
                _ambientOcclusionIntensity = 35;

                _useFancyLightingEngine = true;
                _fancyLightingEngineUseTemporal = true;
                _fancyLightingEngineMakeBrighter = false;
                _fancyLightingEngineLightLoss = 50;

                _useCustomSkyColors = true;

                _threadCount = Environment.ProcessorCount;
            }
            else if (_preset == Preset.QualityPreset)
            {
                _useSmoothLighting = true;
                _useLightMapBlurring = true;
                _lightMapRenderMode = RenderMode.Bicubic;
                _normalMapsStrength = 0;
                _useQualityNormalMaps = false;
                _useFineNormalMaps = false;
                _renderOnlyLight = false;

                _useAmbientOcclusion = true;
                _doNonSolidAmbientOcclusion = true;
                _doTileEntityAmbientOcclusion = true;
                _ambientOcclusionRadius = 4;
                _ambientOcclusionIntensity = 35;

                _useFancyLightingEngine = true;
                _fancyLightingEngineUseTemporal = true;
                _fancyLightingEngineMakeBrighter = true;
                _fancyLightingEngineLightLoss = 50;

                _useCustomSkyColors = true;

                _threadCount = Environment.ProcessorCount;
            }
            else if (_preset == Preset.FastPreset)
            {
                _useSmoothLighting = true;
                _useLightMapBlurring = true;
                _lightMapRenderMode = RenderMode.Bilinear;
                _normalMapsStrength = 0;
                _useQualityNormalMaps = false;
                _useFineNormalMaps = false;
                _renderOnlyLight = false;

                _useAmbientOcclusion = false;
                _doNonSolidAmbientOcclusion = false;
                _doTileEntityAmbientOcclusion = false;
                _ambientOcclusionRadius = 4;
                _ambientOcclusionIntensity = 35;

                _useFancyLightingEngine = false;
                _fancyLightingEngineUseTemporal = true;
                _fancyLightingEngineMakeBrighter = false;
                _fancyLightingEngineLightLoss = 50;

                _useCustomSkyColors = true;

                _threadCount = Environment.ProcessorCount;
            }
            else if (_preset == Preset.UltraPreset)
            {
                _useSmoothLighting = true;
                _useLightMapBlurring = true;
                _lightMapRenderMode = RenderMode.BicubicOverbright;
                _normalMapsStrength = 100;
                _useQualityNormalMaps = true;
                _useFineNormalMaps = false;
                _renderOnlyLight = false;

                _useAmbientOcclusion = true;
                _doNonSolidAmbientOcclusion = true;
                _doTileEntityAmbientOcclusion = true;
                _ambientOcclusionRadius = 4;
                _ambientOcclusionIntensity = 35;

                _useFancyLightingEngine = true;
                _fancyLightingEngineUseTemporal = true;
                _fancyLightingEngineMakeBrighter = true;
                _fancyLightingEngineLightLoss = 50;

                _useCustomSkyColors = true;

                _threadCount = Environment.ProcessorCount;
            }
            else if (_preset == Preset.DisableAllPreset)
            {
                _useSmoothLighting = false;
                _useLightMapBlurring = true;
                _lightMapRenderMode = RenderMode.Bilinear;
                _normalMapsStrength = 0;
                _useQualityNormalMaps = false;
                _useFineNormalMaps = false;
                _renderOnlyLight = false;

                _useAmbientOcclusion = false;
                _doNonSolidAmbientOcclusion = false;
                _doTileEntityAmbientOcclusion = false;
                _ambientOcclusionRadius = 4;
                _ambientOcclusionIntensity = 35;

                _useFancyLightingEngine = false;
                _fancyLightingEngineUseTemporal = true;
                _fancyLightingEngineMakeBrighter = false;
                _fancyLightingEngineLightLoss = 50;

                _useCustomSkyColors = false;

                _threadCount = Environment.ProcessorCount;
            }
            else
            {
                if (
                       _useSmoothLighting
                    && _useLightMapBlurring
                    && _lightMapRenderMode == RenderMode.Bilinear
                    && _normalMapsStrength == 0
                    && !_useQualityNormalMaps
                    && !_useFineNormalMaps
                    && !_renderOnlyLight

                    && _useAmbientOcclusion
                    && _doNonSolidAmbientOcclusion
                    && !_doTileEntityAmbientOcclusion
                    && _ambientOcclusionRadius == 4
                    && _ambientOcclusionIntensity == 35

                    && _useFancyLightingEngine
                    && _fancyLightingEngineUseTemporal
                    && !_fancyLightingEngineMakeBrighter
                    && _fancyLightingEngineLightLoss == 50

                    && _useCustomSkyColors

                    && _threadCount == Environment.ProcessorCount
                )
                {
                    _preset = Preset.DefaultPreset;
                }
                else if (
                       _useSmoothLighting
                    && _useLightMapBlurring
                    && _lightMapRenderMode == RenderMode.Bicubic
                    && _normalMapsStrength == 0
                    && !_useQualityNormalMaps
                    && !_useFineNormalMaps
                    && !_renderOnlyLight

                    && _useAmbientOcclusion
                    && _doNonSolidAmbientOcclusion
                    && _doTileEntityAmbientOcclusion
                    && _ambientOcclusionRadius == 4
                    && _ambientOcclusionIntensity == 35

                    && _useFancyLightingEngine
                    && _fancyLightingEngineUseTemporal
                    && _fancyLightingEngineMakeBrighter
                    && _fancyLightingEngineLightLoss == 50

                    && _useCustomSkyColors

                    && _threadCount == Environment.ProcessorCount
                )
                {
                    _preset = Preset.QualityPreset;
                }
                else if (
                       _useSmoothLighting
                    && _useLightMapBlurring
                    && _lightMapRenderMode == RenderMode.Bilinear
                    && _normalMapsStrength == 0
                    && !_useQualityNormalMaps
                    && !_useFineNormalMaps
                    && !_renderOnlyLight

                    && !_useAmbientOcclusion
                    && !_doNonSolidAmbientOcclusion
                    && !_doTileEntityAmbientOcclusion
                    && _ambientOcclusionRadius == 4
                    && _ambientOcclusionIntensity == 35

                    && !_useFancyLightingEngine
                    && _fancyLightingEngineUseTemporal
                    && !_fancyLightingEngineMakeBrighter
                    && _fancyLightingEngineLightLoss == 50

                    && _useCustomSkyColors

                    && _threadCount == Environment.ProcessorCount
                )
                {
                    _preset = Preset.FastPreset;
                }
                else if (
                       _useSmoothLighting
                    && _useLightMapBlurring
                    && _lightMapRenderMode == RenderMode.BicubicOverbright
                    && _normalMapsStrength == 100
                    && _useQualityNormalMaps
                    && !_useFineNormalMaps
                    && !_renderOnlyLight

                    && _useAmbientOcclusion
                    && _doNonSolidAmbientOcclusion
                    && _doTileEntityAmbientOcclusion
                    && _ambientOcclusionRadius == 4
                    && _ambientOcclusionIntensity == 35

                    && _useFancyLightingEngine
                    && _fancyLightingEngineUseTemporal
                    && _fancyLightingEngineMakeBrighter
                    && _fancyLightingEngineLightLoss == 50

                    && _useCustomSkyColors

                    && _threadCount == Environment.ProcessorCount
                )
                {
                    _preset = Preset.UltraPreset;
                }
                else if (
                       !_useSmoothLighting
                    && _useLightMapBlurring
                    && _lightMapRenderMode == RenderMode.Bilinear
                    && _normalMapsStrength == 0
                    && !_useQualityNormalMaps
                    && !_useFineNormalMaps
                    && !_renderOnlyLight

                    && !_useAmbientOcclusion
                    && !_doNonSolidAmbientOcclusion
                    && !_doTileEntityAmbientOcclusion
                    && _ambientOcclusionRadius == 4
                    && _ambientOcclusionIntensity == 35

                    && !_useFancyLightingEngine
                    && _fancyLightingEngineUseTemporal
                    && !_fancyLightingEngineMakeBrighter
                    && _fancyLightingEngineLightLoss == 50

                    && !_useCustomSkyColors

                    && _threadCount == Environment.ProcessorCount
                )
                {
                    _preset = Preset.DisableAllPreset;
                }
            }
        }
    }
    private Preset _preset;

    // Smooth Lighting, Normal Maps, Overbright
    [Header("Smooth Lighting")]

    [DefaultValue(true)]
    [Label("Enable Smooth Lighting")]
    [Tooltip("Toggles whether to use smooth lighting\nIf disabled, vanilla lighting visuals are used\nRequires lighting to be set to color")]
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

    [DefaultValue(true)]
    [Label("Blur Light Map")]
    [Tooltip("Toggles whether to blur the light map\nApplies a per-tile blur to the light map before rendering\nSmooths sharp light transitions\nDisabling this setting may slightly increase performance")]
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

    [DrawTicks]
    [DefaultValue(RenderMode.Bilinear)]
    [Label("Light Map Render Mode")]
    [Tooltip("Controls how the light map is rendered\nAffects the smoothness of lighting\nBicubic upscaling is smoother than bilinear upscaling\nOverbright rendering increases the maximum brightness of light")]
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
    [DefaultValue(0)]
    [Slider]
    [DrawTicks]
    [Label("Simulated Normal Maps Strength")]
    [Tooltip("Controls the strength of simulated normal maps\nWhen not 0, tiles have simulated normal maps and appear bumpy\nSet to 0 to disable")]
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

    [DefaultValue(false)]
    [Label("Use Higher-Quality Normal Maps")]
    [Tooltip("Toggles between regular and higher-quality simulated normal map shaders\nWhen enabled, uses a higher-quality normal map simulation\nMay reduce performance when enabled")]
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

    [DefaultValue(false)]
    [Label("Use Fine Normal Maps")]
    [Tooltip("Toggles between coarse and fine simulated normal maps\nCoarse normal maps have 2x2 resolution, and fine 1x1\nRecommended to enable when using HD textures")]
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

    [DefaultValue(false)]
    [Label("(Debug) Render Only Lighting")]
    [Tooltip("When enabled, tiles, walls, and the background aren't rendered")]
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

    [DefaultValue(true)]
    [Label("Enable Ambient Occlusion")]
    [Tooltip("Toggles whether to use ambient occlusion\nIf enabled, tiles produce shadows in front of walls\nRequires lighting to be set to color")]
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

    [DefaultValue(true)]
    [Label("Enable Ambient Occlusion From Non-Solid Tiles")]
    [Tooltip("Toggles whether non-solid blocks generate ambient occlusion\nNon-solid tiles generate weaker ambient occlusion\nPrimarily affects furniture and torches\nNot all non-solid tiles are affected")]
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

    [DefaultValue(true)]
    [Label("Enable Ambient Occlusion From Tile Entities")]
    [Tooltip("Toggles whether tile entities generate ambient occlusion\nTile entities generate weaker ambient occlusion\nPrimarily affects moving, non-solid tiles (e.g., tiles affected by wind)")]
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
    [DefaultValue(4)]
    [Slider]
    [DrawTicks]
    [Label("Ambient Occlusion Radius")]
    [Tooltip("Controls the radius of blur used in ambient occlusion\nHigher values correspond to a larger blur radius\nHigher values may reduce performance")]
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
    [DefaultValue(35)]
    [Slider]
    [DrawTicks]
    [Label("Ambient Occlusion Intensity")]
    [Tooltip("Controls the intensity of shadows in ambient occlusion\nHigher values correspond to darker ambient occlusion shadows")]
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

    [DefaultValue(true)]
    [Label("Enable Fancy Lighting Engine")]
    [Tooltip("Toggles whether to use a modified lighting engine\nWhen enabled, light is spread more accurately\nShadows should face away from light sources and be more noticeable\nPerformance is significantly reduced in areas with more light sources\nRequires lighting to be set to color")]
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

    [DefaultValue(true)]
    [Label("Temporal Optimization")]
    [Tooltip("Toggles whether to use temporal optimization with the fancy lighting engine\nWhen enabled, uses data from the previous update to optimize lighting calculations\nMakes lighting quicker in more intensly lit areas\nMay sometimes cause lighting quality to be slightly reduced")]
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

    [DefaultValue(false)]
    [Label("Brighter Lighting")]
    [Tooltip("Toggles whether to make lighting slightly brighter\nWhen disabled, lighting is slightly darker than with vanilla lighting\nMay reduce performance when enabled")]
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

    [Range(0, 65)]
    [Increment(5)]
    [DefaultValue(50)]
    [Slider]
    [DrawTicks]
    [Label("Light Loss (%) When Exiting Solid Blocks")]
    [Tooltip("Controls how much light is lost when light exits a solid block into the air\nHigher values correspond to darker shadows")]
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

    // Sky Color
    [Header("Sky Color")]

    [DefaultValue(true)]
    [Label("Enable Fancy Sky Colors")]
    [Tooltip("Toggles whether to use modified sky colors\nIf disabled, vanilla sky colors are used instead")]
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

    // Other Settings
    [Header("General")]

    [Range(1, 24)]
    [Increment(1)]
    [DefaultValue(8)]
    [Label("Thread Count")]
    [Tooltip("Controls how many threads smooth lighting and the fancy lighting engine use\nThe default value should result in the best performance")]
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

    public override void OnChanged()
    {
        ModContent.GetInstance<FancyLightingModSystem>()?.UpdateSettings();

        base.OnChanged();
    }
}
