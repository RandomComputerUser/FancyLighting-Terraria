using System;

namespace FancyLighting.Util;

internal static class MathUtil
{
    public static double Integrate(
        Func<double, double> fun, double lowerBound, double upperBound, int steps = 64
    )
    {
        // Simpson's rule

        if (lowerBound == upperBound)
        {
            return 0.0;
        }

        if (steps <= 0)
        {
            steps = 64;
        }

        double sum = fun(lowerBound);

        double middleOffset = (upperBound - lowerBound) * 0.5 / steps;
        for (int i = 1; i < steps; ++i)
        {
            double t = (double)i / steps;
            double x = (1 - t) * lowerBound + t * upperBound;
            sum += 4.0 * fun(x - middleOffset) + 2.0 * fun(x);
        }

        sum += 4.0 * fun(upperBound - middleOffset) + fun(upperBound);
        return (upperBound - lowerBound) * sum / (6.0 * steps);
    }

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
