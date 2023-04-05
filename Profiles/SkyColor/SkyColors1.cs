using Microsoft.Xna.Framework;

namespace FancyLighting.Profiles.SkyColor;

public class SkyColors1 : ISimpleColorProfile
{
    private SkyColorProfile _profile;

    public SkyColors1()
    {
        // Attempts to be more realistic

        _profile = new(InterpolationMode.Cubic);

        double noonTime = 12.0;
        double sunriseTime = noonTime - 7.25;
        double sunsetTime = noonTime + 7.25;

        Vector3 midnightColor
            = new(0.025f, 0.030f, 0.040f);
        Vector3 nightColor
            = new(0.030f, 0.035f, 0.045f);
        Vector3 twilightColor1
            = new(0.040f, 0.050f, 0.060f);
        Vector3 twilightColor2
            = new(0.080f, 0.070f, 0.100f);
        Vector3 sunrisesetColor
            = new(0.180f, 0.100f, 0.150f);
        Vector3 goldenHourColor1
            = new(0.360f, 0.160f, 0.210f);
        Vector3 goldenHourColor2
            = new(0.600f, 0.500f, 0.360f);
        Vector3 dayColor1
            = new(0.750f, 0.700f, 0.600f);
        Vector3 dayColor2
            = new(0.900f, 0.900f, 1.100f);
        Vector3 noonColor
            = new(3.000f, 3.000f, 3.000f);

        (double hour, Vector3 color)[] colors = new[]
        {
            (0.0, midnightColor),
            (sunriseTime - 1.5, nightColor),
            (sunriseTime - 1.0, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.5, goldenHourColor1),
            (sunriseTime + 1.5, goldenHourColor2),
            (sunriseTime + 2.0, dayColor1),
            (sunriseTime + 2.5, dayColor2),
            (noonTime, noonColor),
            (sunsetTime - 2.5, dayColor2),
            (sunsetTime - 2.0, dayColor1),
            (sunsetTime - 1.5, goldenHourColor2),
            (sunsetTime - 0.5, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.0, twilightColor1),
            (sunsetTime + 1.5, nightColor),
            (24.0, midnightColor)
        };

        foreach ((double hour, Vector3 color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
