using Terraria.ModLoader.Config;

namespace FancyLighting.Config
{
    public enum RenderMode : int
    {
        [Label("Bilinear Upscaling")]
        Bilinear,
        [Label("Bicubic Upscaling")]
        Bicubic,
        [Label("Bicubic with Overbright")]
        BicubicOverbright
    }
}
