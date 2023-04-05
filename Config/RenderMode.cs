using Terraria.ModLoader.Config;

namespace FancyLighting.Config;

public enum RenderMode : int
{
    [Label("Bilinear Upscaling")]
    Bilinear = 0,
    [Label("Bicubic Upscaling")]
    Bicubic = 1,
    [Label("Bicubic with Overbright")]
    BicubicOverbright = 2
}
