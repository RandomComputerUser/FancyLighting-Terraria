using System;

namespace FancyLighting.Util;

internal static class MathUtil
{
    public static double Hypot(double x, double y)
    {
        x = Math.Abs(x);
        y = Math.Abs(y);

        if (x == 0.0)
        {
            return y;
        }
        if (y == 0.0)
        {
            return x;
        }

        double big = Math.Max(x, y);
        double small = Math.Min(x, y);

        double ratio = small / big;

        return big * Math.Sqrt(1.0 + ratio * ratio);
    }
}
