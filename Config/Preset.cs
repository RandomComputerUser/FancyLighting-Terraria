using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public enum Preset : int
{
    [Label("Custom")]
    CustomPreset = 10,
    [Label("Vanilla")]
    VanillaPreset = 20,
    [Label("Low")]
    LowPreset = 30,
    [Label("Medium")]
    MediumPreset = 40,
    [Label("High")]
    HighPreset = 50,
    [Label("Very High")]
    VeryHighPreset = 55,
    [Label("Ultra")]
    UltraPreset = 60
}
