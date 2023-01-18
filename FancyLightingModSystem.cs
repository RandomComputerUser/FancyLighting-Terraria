using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FancyLighting;

public sealed class FancyLightingModSystem : ModSystem
{
    private readonly Config.LightingConfig _configInstance;

    private FancyLightingModSystem() : base()
    {
        _configInstance = ModContent.GetInstance<Config.LightingConfig>();
    }

    public override void OnWorldLoad()
    {
        if (Main.netMode == NetmodeID.Server)
        {
            return;
        }

        SmoothLighting SmoothLightingObj = ModContent.GetInstance<FancyLightingMod>()?._smoothLightingInstance;
        if (SmoothLightingObj is not null)
        {
            SmoothLightingObj._printExceptionTime = 60;
        }

        base.OnWorldLoad();
    }

    internal void UpdateSettings()
    {
        FancyLightingMod._smoothLightingEnabled = _configInstance.UseSmoothLighting && Lighting.UsingNewLighting;
        FancyLightingMod._blurLightMap = _configInstance.UseLightMapBlurring;
        FancyLightingMod._lightMapRenderMode = _configInstance.LightMapRenderMode;
        FancyLightingMod._normalMapsStrength = _configInstance.NormalMapsStrength;
        FancyLightingMod._useQualityNormalMaps = _configInstance.QualityNormalMaps;
        FancyLightingMod._useFineNormalMaps = _configInstance.FineNormalMaps;
        FancyLightingMod._renderOnlyLight = _configInstance.RenderOnlyLight;

        FancyLightingMod._ambientOcclusionEnabled = _configInstance.UseAmbientOcclusion && Lighting.UsingNewLighting;
        FancyLightingMod._ambientOcclusionNonSolid = _configInstance.DoNonSolidAmbientOcclusion;
        FancyLightingMod._ambientOcclusionTileEntity = _configInstance.DoTileEntityAmbientOcclusion;
        FancyLightingMod._ambientOcclusionRadius = _configInstance.AmbientOcclusionRadius;
        FancyLightingMod._ambientOcclusionIntensity = _configInstance.AmbientOcclusionIntensity;

        FancyLightingMod._fancyLightingEngineEnabled = _configInstance.UseFancyLightingEngine && Lighting.UsingNewLighting;
        FancyLightingMod._fancyLightingEngineUseTemporal = _configInstance.FancyLightingEngineUseTemporal;
        FancyLightingMod._fancyLightingEngineLightLoss = _configInstance.FancyLightingEngineLightLoss;
        FancyLightingMod._fancyLightingEngineMakeBrighter = _configInstance.FancyLightingEngineMakeBrighter;

        FancyLightingMod._skyColorsEnabled = _configInstance.UseCustomSkyColors && Lighting.UsingNewLighting;

        FancyLightingMod._threadCount = _configInstance.ThreadCount;
    }
}
