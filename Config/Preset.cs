using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public enum Preset : int
{
    [Label("Custom")]
    CustomPreset = 10,
    [Label("Disable All")]
    DisableAllPreset = 20,
    [Label("Fast")]
    FastPreset = 30,
    [Label("Default")]
    DefaultPreset = 40,
    [Label("Quality")]
    QualityPreset = 50,
    [Label("Ultra")]
    UltraPreset = 60
}
