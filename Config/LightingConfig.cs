using System;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config
{
    [Label("Fancy Lighting Settings")]
    public class LightingConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        // Smooth Lighting

        [Header("Smooth Lighting")]
        [DefaultValue(true)]
        [Label("Enable Smooth Lighting")]
        [Tooltip("Toggles whether or not to use smooth lighting\nIf disabled, vanilla lighting visuals are used\nRequires lighting to be set to color")]
        public bool UseSmoothLighting
        {
            get
            {
                return _useSmoothLighting;
            }
            set
            {
                _useSmoothLighting = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _useSmoothLighting;

        [DefaultValue(true)]
        [Label("Blur Light Map")]
        [Tooltip("Toggles whether or not to blur the light map\nApplies a per-tile blur to the light map before rendering\nSmooths jagged corners in the light map\nDisabling this setting may slightly increase performance")]
        public bool UseLightMapBlurring
        {
            get
            {
                return _useLightMapBlurring;
            }
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
            get
            {
                return _lightMapRenderMode;
            }
            set
            {
                _lightMapRenderMode = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private RenderMode _lightMapRenderMode;

        [DefaultValue(false)]
        [Label("Simulate Normal Maps")]
        [Tooltip("Toggles whether or not to simulate normal maps\nWhen enabled, tiles have simulated normal maps and appear bumpy")]
        public bool SimulateNormalMaps
        {
            get
            {
                return _simulateNormalMaps;
            }
            set
            {
                _simulateNormalMaps = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _simulateNormalMaps;

        // Ambient Occlusion

        [DefaultValue(false)]
        [Label("(Debug) Render Only Lighting")]
        [Tooltip("When enabled, tiles, walls, and the background aren't rendered")]
        public bool RenderOnlyLight
        {
            get
            {
                return _renderOnlyLight;
            }
            set
            {
                _renderOnlyLight = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _renderOnlyLight;

        [Header("Ambient Occlusion")]
        [DefaultValue(true)]
        [Label("Enable Ambient Occlusion")]
        [Tooltip("Toggles whether or not to use ambient occlusion\nIf enabled, shadows are added around the edges of foreground tiles in front of background walls\nRequires lighting to be set to color")]
        public bool UseAmbientOcclusion
        {
            get
            {
                return _useAmbientOcclusion;
            }
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
            get
            {
                return _doNonSolidAmbientOcclusion;
            }
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
            get
            {
                return _doTileEntityAmbientOcclusion;
            }
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
        [Tooltip("Controls the radius of blur used in ambient occlusion\nHigher values correspond to a larger blur radius\nHigher values may degrade performance")]
        public int AmbientOcclusionRadius
        {
            get
            {
                return _ambientOcclusionRadius;
            }
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
            get
            {
                return _ambientOcclusionIntensity;
            }
            set
            {
                _ambientOcclusionIntensity = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private int _ambientOcclusionIntensity;

        // Lighting Engine

        [Header("Lighting Engine")]
        [DefaultValue(true)]
        [Label("Enable Fancy Lighting Engine")]
        [Tooltip("Toggles whether or not to use a modified lighting engine\nWhen enabled, light is spread more accurately\nShadows should face away from light sources and be more noticeable\nPerformance is significantly reduced in areas with more light sources\nRequires lighting to be set to color")]
        public bool UseFancyLightingEngine
        {
            get
            {
                return _useFancyLightingEngine;
            }
            set
            {
                _useFancyLightingEngine = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _useFancyLightingEngine;

        [DefaultValue(true)]
        [Label("Temporal Optimization")]
        [Tooltip("Toggles whether or not to use temporal optimization with the fancy lighting engine\nWhen enabled, data from the previous update is used to optimize lighting during the current update\nMakes lighting quicker in more intensly lit areas\nMay sometimes result in a slightly lowered lighting quality")]
        public bool FancyLightingEngineUseTemporal
        {
            get
            {
                return _fancyLightingEngineUseTemporal;
            }
            set
            {
                _fancyLightingEngineUseTemporal = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _fancyLightingEngineUseTemporal;

        [DefaultValue(false)]
        [Label("Brighter Lighting")]
        [Tooltip("Toggles whether or not to make lighting slightly brighter\nWhen disabled, lighting is slightly darker than when using the vanilla lighting engine\nSlightly degrades performance when enabled")]
        public bool FancyLightingEngineMakeBrighter
        {
            get
            {
                return _fancyLightingEngineMakeBrighter;
            }
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
            get
            {
                return _fancyLightingEngineLightLoss;
            }
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
        [Tooltip("Toggles whether or not to use modified sky colors\nIf disabled, vanilla sky colors are used instead")]
        public bool UseCustomSkyColors
        {
            get
            {
                return _useCustomSkyColors;
            }
            set
            {
                _useCustomSkyColors = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private bool _useCustomSkyColors;

        // General

        [Header("General")]
        [Range(1, 24)]
        [Increment(1)]
        [DefaultValue(8)]
        [Label("Thread Count")]
        [Tooltip("Controls how many threads smooth lighting and the fancy lighting engine use\nThe default value should result in the best performance")]
        public int ThreadCount
        {
            get
            {
                return _threadCount;
            }
            set
            {
                _threadCount = value;
                ConfigPreset = Preset.CustomPreset;
            }
        }
        private int _threadCount;

        // Presets

        [Header("Presets")]
        // I really wish enums would use a dropdown menu UI, but sliders are used instead
        // I couldn't find a dropdown menu config UI built into tModLoader
        [DrawTicks]
        [DefaultValue(Preset.DefaultPreset)]
        [Label("Settings Preset")]
        [Tooltip("A preset for this settings page can be set here")]
        public Preset ConfigPreset
        {
            get
            {
                return _preset;
            }
            set
            {
                _preset = value;

                // Bad code ahead

                if (_preset == Preset.DefaultPreset)
                {
                    _useSmoothLighting = true;
                    _useLightMapBlurring = true;
                    _lightMapRenderMode = RenderMode.Bilinear;
                    _simulateNormalMaps = false;
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
                    _simulateNormalMaps = false;
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
                    _simulateNormalMaps = false;
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
                    _simulateNormalMaps = true;
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
                    _simulateNormalMaps = false;
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
                        && !_simulateNormalMaps
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
                        && !_simulateNormalMaps
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
                        && !_simulateNormalMaps
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
                        && _simulateNormalMaps
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
                        && !_simulateNormalMaps
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

        public override void OnChanged()
        {
            ModContent.GetInstance<FancyLightingModSystem>()?.UpdateSettings();

            base.OnChanged();
        }
    }
}
