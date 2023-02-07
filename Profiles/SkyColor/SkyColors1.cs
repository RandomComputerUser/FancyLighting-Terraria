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

        Vector3 midnightColor = new(0.03f, 0.04f, 0.05f);
        Vector3 nightColor = new(0.035f, 0.045f, 0.055f);
        Vector3 twilightColor1 = new(0.05f, 0.06f, 0.08f);
        Vector3 twilightColor2 = new(0.1f, 0.08f, 0.13f);
        Vector3 sunrisesetColor = new(0.23f, 0.1f, 0.18f);
        Vector3 goldenHourColor1 = new(0.5f, 0.22f, 0.23f);
        Vector3 goldenHourColor2 = new(0.7f, 0.6f, 0.29f);
        Vector3 dayColor1 = new(0.8f, 0.75f, 0.45f);
        Vector3 dayColor2 = new(0.9f, 0.87f, 0.9f);
        Vector3 noonColor = new(1.5f, 1.65f, 1.9f);

        (double hour, Vector3 color)[] colors = new[]
        {
            (0.0, midnightColor),
            (sunriseTime - 1.35, nightColor),
            (sunriseTime - 1.0, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.5, goldenHourColor1),
            (sunriseTime + 1.25, goldenHourColor2),
            (sunriseTime + 1.6, dayColor1),
            (sunriseTime + 2.1, dayColor2),
            (noonTime, noonColor),
            (sunsetTime - 2.1, dayColor2),
            (sunsetTime - 1.6, dayColor1),
            (sunsetTime - 1.25, goldenHourColor2),
            (sunsetTime - 0.5, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.0, twilightColor1),
            (sunsetTime + 1.35, nightColor),
            (24.0, midnightColor)
        };

        foreach ((double hour, Vector3 color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
