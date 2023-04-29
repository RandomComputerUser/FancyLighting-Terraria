using Microsoft.Xna.Framework;
using System;

namespace FancyLighting.Util;

internal static class SrgbConverter
{
    public static void SrgbToLinear(ref Vector3 color)
    {
        // Intentionally not the standard sRGB conversion
        // The linear function for low values wouldn't make sense, I think

        // Using MathF.Sqrt() instead of MathF.Pow() gives us
        // better performance and a gamma of 2.25 (close to 2.2)]
        color.X *= MathF.Sqrt(color.X);
        color.X *= MathF.Sqrt(color.X);
        color.Y *= MathF.Sqrt(color.Y);
        color.Y *= MathF.Sqrt(color.Y);
        color.Z *= MathF.Sqrt(color.Z);
        color.Z *= MathF.Sqrt(color.Z);
    }

    public static float SrgbToLinear(float brightness)
    {
        brightness *= MathF.Sqrt(brightness);
        brightness *= MathF.Sqrt(brightness);
        return brightness;
    }

    public static void LinearToSrgb(ref Vector3 color)
    {
        // This function exists so that the game doesn't render dark areas
        // as completely black, as that feature was adjusted for sRGB

        // MathF.Exp() might be slightly faster than MathF.Pow()?
        color.X = MathF.Exp(MathF.Log(color.X) * (1 / 2.25f));
        color.Y = MathF.Exp(MathF.Log(color.Y) * (1 / 2.25f));
        color.Z = MathF.Exp(MathF.Log(color.Z) * (1 / 2.25f));
    }
}
