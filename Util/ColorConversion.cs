using Microsoft.Xna.Framework;

namespace FancyLighting.Util;

public static class ColorConversion
{
    // Provide better conversions from Vector3 to Color than XNA
    // XNA uses (byte)(x * 255f) for each component

    public static void Assign(ref Color color, float overbrightMult, Vector3 rgb)
    {
        color.R = (byte)(255f * MathHelper.Clamp(overbrightMult * rgb.X, 0f, 1f) + 0.5f);
        color.G = (byte)(255f * MathHelper.Clamp(overbrightMult * rgb.Y, 0f, 1f) + 0.5f);
        color.B = (byte)(255f * MathHelper.Clamp(overbrightMult * rgb.Z, 0f, 1f) + 0.5f);
        color.A = byte.MaxValue;
    }
}
