using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace FancyLighting.Util;

internal static class ToneMapper
{
    public const float WHITE_POINT = 1.25f;

    // Extended Reinhard Tone Mapping using luminance
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ToneMap(ref Vector3 color)
    {
        float luminance = 0.2126f * color.X + 0.7152f * color.Y + 0.0722f * color.Z;
        float mult =
            (1f + luminance * (1f / WHITE_POINT / WHITE_POINT)) / (1f + luminance);
        Vector3.Multiply(ref color, mult, out color);
    }
}
