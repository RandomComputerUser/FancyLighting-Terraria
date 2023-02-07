using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace FancyLighting.Profiles.SkyColor;

public class SkyColorProfile : ISimpleColorProfile
{
    protected List<(double hour, Vector3 color)> _colors;
    protected InterpolationMode _interpolationMode;

    public SkyColorProfile(InterpolationMode interpolationMode)
    {
        _colors = new();
        _interpolationMode = interpolationMode;
    }

    public virtual void AddColor(double hour, Vector3 color) => _colors.Add((hour, color));

    protected (double hour, Vector3 color) HourColorAtIndex(int index)
    {
        if (index < 0)
        {
            (double hour, Vector3 color) = _colors[index + _colors.Count];
            return (hour - 24.0, color);
        }
        if (index >= _colors.Count)
        {
            (double hour, Vector3 color) = _colors[index - _colors.Count];
            return (hour + 24.0, color);
        }
        return _colors[index];
    }

    private static Vector3 ChooseDerivative(double x1, double x2, double x3, Vector3 y1, Vector3 y2, Vector3 y3)
    {
        Vector3 baseSlope = (y3 - y1) / (float)(x3 - x1);
        Vector3 slope1 = 2 * (y2 - y1) / (float)(x2 - x1);
        Vector3 slope2 = 2 * (y3 - y2) / (float)(x3 - x2);

        static float derivative(float baseSlope, float slope1, float slope2)
        {
            float minSlope = 0f;
            float maxSlope = 0f;

            if (slope1 > 0f)
            {
                if (slope2 > 0f)
                {
                    maxSlope = MathHelper.Min(slope1, slope2);
                }
                else
                {
                    minSlope = slope2;
                    maxSlope = slope1;
                }
            }
            else
            {
                if (slope2 > 0f)
                {
                    minSlope = slope1;
                    maxSlope = slope2;
                }
                else
                {
                    minSlope = MathHelper.Max(slope1, slope2);
                }
            }

            return MathHelper.Clamp(baseSlope, minSlope, maxSlope);
        }

        return new(
            derivative(baseSlope.X, slope1.X, slope2.X),
            derivative(baseSlope.Y, slope1.Y, slope2.Y),
            derivative(baseSlope.Z, slope1.Z, slope2.Z)
        );
    }

    public virtual Vector3 GetColor(double hour)
    {
        hour %= 24.0;

        for (int i = 1; i < _colors.Count; ++i)
        {
            if (hour > _colors[i].hour)
            {
                continue;
            }

            (double hour1, Vector3 color1) = HourColorAtIndex(i - 2);
            (double hour2, Vector3 color2) = HourColorAtIndex(i - 1);
            (double hour3, Vector3 color3) = HourColorAtIndex(i);
            (double hour4, Vector3 color4) = HourColorAtIndex(i + 1);

            switch (_interpolationMode)
            {
            case InterpolationMode.Linear:
                // Linear interpolation

                float t = (float)((hour - hour2) / (hour3 - hour2));
                return (1 - t) * color2 + t * color3;
            case InterpolationMode.Cubic:
            default:
                // Cubic Hermite spline interpolation

                Vector3 y1 = color2;
                Vector3 m1 = ChooseDerivative(hour1, hour2, hour3, color1, color2, color3);

                float x2 = (float)(hour3 - hour2);
                float x2_2 = x2 * x2;
                Vector3 y2 = color3;
                Vector3 m2 = ChooseDerivative(hour2, hour3, hour4, color2, color3, color4);

                Vector3 y_diff = y2 - y1;

                Vector3 a = 2 * y_diff - x2 * (m1 + m2);
                Vector3 b = x2_2 * m2 + 2 * x2_2 * m1 - 3 * x2 * y_diff;
                Vector3 c = m1;
                Vector3 d = y1;

                float den = -x2_2 * x2;
                a /= den;
                b /= den;

                float x = (float)(hour - hour2);
                return Vector3.Min(d + x * (c + x * (b + x * a)), Vector3.One);
            }
        }

        return _colors[^1].color;
    }
}
