using Terraria.ModLoader.Config;

using System.ComponentModel;

namespace FancyLighting.Config
{
    public enum Preset : int
    {
        [Label("Default")]
        DefaultPreset,
        [Label("Quality")]
        QualityPreset,
        [Label("Fast")]
        FastPreset,
        [Label("Custom")]
        CustomPreset
    }
}