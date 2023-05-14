using Microsoft.Xna.Framework;
using System;

namespace FancyLighting.Util;

internal static class GammaConverter
{
    // Uses a gamma of 2.25

    public static void GammaToLinear(ref Vector3 color)
    {
        color.X *= color.X;
        color.Y *= color.Y;
        color.Z *= color.Z;
    }

    public static void LinearToSrgb(ref Vector3 color)
    {
        color.X = color.X <= 0.0031308f
            ? 12.92f * color.X
            : 1.055f * MathF.Pow(color.X, 1f / 2.4f) - 0.055f;
        color.Y = color.Y <= 0.0031308f
            ? 12.92f * color.Y
            : 1.055f * MathF.Pow(color.Y, 1f / 2.4f) - 0.055f;
        color.Z = color.Z <= 0.0031308f
            ? 12.92f * color.Z
            : 1.055f * MathF.Pow(color.Z, 1f / 2.4f) - 0.055f;
    }
}
