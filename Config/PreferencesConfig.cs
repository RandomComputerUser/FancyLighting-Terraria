using System;
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public sealed class PreferencesConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    // Handled automatically by tModLoader
    public static PreferencesConfig Instance;

    internal bool UseNormalMaps() => NormalMapsStrength != 0;

    internal float NormalMapsMultiplier() => NormalMapsStrength / 100f;

    internal float AmbientOcclusionPower() => AmbientOcclusionIntensity / 50f;

    internal float AmbientOcclusionMult() => AmbientLightProportion / 100f;

    internal float FancyLightingEngineExitMultiplier() =>
        1f - FancyLightingEngineLightLoss / 100f;

    internal float FancyLightingEngineAbsorptionExponent() =>
        FancyLightingEngineLightAbsorption / 100f;

    internal bool CustomSkyColorsEnabled() =>
        UseCustomSkyColors && Lighting.UsingNewLighting;

    public override void OnChanged() =>
        ModContent.GetInstance<FancyLightingMod>()?.OnConfigChange();

    [Range(DefaultOptions.MinThreadCount, DefaultOptions.MaxThreadCount)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.ThreadCount)]
    public int ThreadCount
    {
        get => _threadCount;
        set
        {
            _threadCount =
                value is DefaultOptions.ThreadCount
                    ? DefaultOptions.RuntimeDefaultThreadCount
                    : value;
        }
    }
    private int _threadCount;

    // Smooth Lighting, Normal Maps, Overbright
    [Header("SmoothLighting")]
    [DefaultValue(DefaultOptions.UseLightMapToneMapping)]
    public bool UseLightMapToneMapping
    {
        get => _useLightMapToneMapping;
        set { _useLightMapToneMapping = value; }
    }
    private bool _useLightMapToneMapping;

    [Range(25, 200)]
    [Increment(25)]
    [DefaultValue(DefaultOptions.NormalMapsStrength)]
    [Slider]
    [DrawTicks]
    public int NormalMapsStrength
    {
        get => _normalMapsStrength;
        set { _normalMapsStrength = value; }
    }
    private int _normalMapsStrength;

    [DefaultValue(DefaultOptions.FineNormalMaps)]
    public bool FineNormalMaps
    {
        get => _useFineNormalMaps;
        set { _useFineNormalMaps = value; }
    }
    private bool _useFineNormalMaps;

    [DefaultValue(DefaultOptions.RenderOnlyLight)]
    public bool RenderOnlyLight
    {
        get => _renderOnlyLight;
        set { _renderOnlyLight = value; }
    }
    private bool _renderOnlyLight;

    // Ambient Occlusion
    [Header("AmbientOcclusion")]
    [Range(1, 4)]
    [Increment(1)]
    [DefaultValue(DefaultOptions.AmbientOcclusionRadius)]
    [Slider]
    [DrawTicks]
    public int AmbientOcclusionRadius
    {
        get => _ambientOcclusionRadius;
        set { _ambientOcclusionRadius = value; }
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
        set { _ambientOcclusionIntensity = value; }
    }
    private int _ambientOcclusionIntensity;

    [Range(5, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.AmbientLightProportion)]
    [Slider]
    [DrawTicks]
    public int AmbientLightProportion
    {
        get => _ambientLightProportion;
        set { _ambientLightProportion = value; }
    }
    private int _ambientLightProportion;

    // Fancy Lighting Engine
    [Header("LightingEngine")]
    [Range(0, 100)]
    [Increment(5)]
    [DefaultValue(DefaultOptions.FancyLightingEngineLightLoss)]
    [Slider]
    [DrawTicks]
    public int FancyLightingEngineLightLoss
    {
        get => _fancyLightingEngineLightLoss;
        set { _fancyLightingEngineLightLoss = value; }
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
        set { _fancyLightingEngineLightAbsorption = value; }
    }
    private int _fancyLightingEngineLightAbsorption;

    // Sky Color
    [Header("SkyColor")]
    [DefaultValue(DefaultOptions.UseCustomSkyColors)]
    public bool UseCustomSkyColors
    {
        get => _useCustomSkyColors;
        set { _useCustomSkyColors = value; }
    }
    private bool _useCustomSkyColors;

    [DefaultValue(DefaultOptions.CustomSkyPreset)]
    [DrawTicks]
    public SkyColorPreset CustomSkyPreset
    {
        get => _customSkyPreset;
        set { _customSkyPreset = value; }
    }
    private SkyColorPreset _customSkyPreset;
}
