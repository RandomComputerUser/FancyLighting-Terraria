using Terraria.ModLoader.Config;

namespace FancyLighting.Config
{
    public enum Preset : int
    {
        [Label("Default")]
        DefaultPreset = 0,
        [Label("Quality")]
        QualityPreset = 1,
        [Label("Fast")]
        FastPreset = 2,
        [Label("Ultra")]
        UltraPreset = 3,
        [Label("Disable All")]
        DisableAllPreset = 4,
        [Label("Custom")]
        CustomPreset = 5
    }
}
