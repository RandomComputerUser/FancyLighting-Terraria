using System.Collections.Generic;
using Microsoft.Xna.Framework;

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

    public virtual void AddColor(double hour, Vector3 color) =>
        _colors.Add((hour, color));

    protected (double hour, Vector3 color) HourColorAtIndex(int index)
    {
        if (index < 0)
        {
            (var hour, var color) = _colors[index + _colors.Count];
            return (hour - 24.0, color);
        }
        if (index >= _colors.Count)
        {
            (var hour, var color) = _colors[index - _colors.Count];
            return (hour + 24.0, color);
        }
        return _colors[index];
    }

    private static Vector3 ChooseDerivative(
        double x1,
        double x2,
        double x3,
        Vector3 y1,
        Vector3 y2,
        Vector3 y3
    )
    {
        var baseSlope = (y3 - y1) / (float)(x3 - x1);
        var slope1 = 2 * (y2 - y1) / (float)(x2 - x1);
        var slope2 = 2 * (y3 - y2) / (float)(x3 - x2);

        static float derivative(float baseSlope, float slope1, float slope2)
        {
            var minSlope = 0f;
            var maxSlope = 0f;

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

        for (var i = 1; i < _colors.Count; ++i)
        {
            if (hour > _colors[i].hour)
            {
                continue;
            }

            (var hour1, var color1) = HourColorAtIndex(i - 2);
            (var hour2, var color2) = HourColorAtIndex(i - 1);
            (var hour3, var color3) = HourColorAtIndex(i);
            (var hour4, var color4) = HourColorAtIndex(i + 1);

            switch (_interpolationMode)
            {
                case InterpolationMode.Linear:
                    // Linear interpolation

                    var t = (float)((hour - hour2) / (hour3 - hour2));
                    return (1 - t) * color2 + t * color3;
                case InterpolationMode.Cubic:
                default:
                    // Cubic Hermite spline interpolation

                    var y1 = color2;
                    var m1 = ChooseDerivative(
                        hour1,
                        hour2,
                        hour3,
                        color1,
                        color2,
                        color3
                    );

                    var x2 = (float)(hour3 - hour2);
                    var x2_2 = x2 * x2;
                    var y2 = color3;
                    var m2 = ChooseDerivative(
                        hour2,
                        hour3,
                        hour4,
                        color2,
                        color3,
                        color4
                    );

                    var y_diff = y2 - y1;

                    var a = 2 * y_diff - x2 * (m1 + m2);
                    var b = x2_2 * m2 + 2 * x2_2 * m1 - 3 * x2 * y_diff;
                    var c = m1;
                    var d = y1;

                    var den = -x2_2 * x2;
                    a /= den;
                    b /= den;

                    var x = (float)(hour - hour2);
                    return Vector3.Min(d + x * (c + x * (b + x * a)), Vector3.One);
            }
        }

        return _colors[^1].color;
    }
}
