using Microsoft.Xna.Framework;
using System;
using System.Runtime.CompilerServices;

namespace FancyLighting.Util;

internal static class GammaConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref float x) => x = MathF.Pow(x, 2.2f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GammaToLinear(ref Vector3 color)
    {
        GammaToLinear(ref color.X);
        GammaToLinear(ref color.Y);
        GammaToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToGamma(ref float x) => x = MathF.Pow(x, 1f / 2.2f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToGamma(ref Vector3 color)
    {
        LinearToGamma(ref color.X);
        LinearToGamma(ref color.Y);
        LinearToGamma(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SrgbToLinear(ref float x)
        => x = x <= 0.04045f
            ? (1f / 12.92f) * x
            : MathF.Pow((1f / 1.055f) * (x + 0.055f), 2.4f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SrgbToLinear(ref Vector3 color)
    {
        SrgbToLinear(ref color.X);
        SrgbToLinear(ref color.Y);
        SrgbToLinear(ref color.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToSrgb(ref float x)
        => x = x <= 0.0031308f
            ? 12.92f * x
            : 1.055f * MathF.Pow(x, 1f / 2.4f) - 0.055f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinearToSrgb(ref Vector3 color)
    {
        LinearToSrgb(ref color.X);
        LinearToSrgb(ref color.Y);
        LinearToSrgb(ref color.Z);
    }
}
