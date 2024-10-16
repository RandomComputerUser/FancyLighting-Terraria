using Microsoft.Xna.Framework;

namespace FancyLighting.Profiles.SkyColor;

public class SkyColors1 : ISimpleColorProfile
{
    private SkyColorProfile _profile;

    public SkyColors1()
    {
        // Attempts to be more "balanced"

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - (7 + 1.0 / 3.0);
        var sunsetTime = noonTime + (7 + 1.0 / 3.0);

        var midnightColor = new Vector3(0.018f, 0.027f, 0.054f);
        var nightColor = new Vector3(0.020f, 0.030f, 0.060f);
        var twilightColor1 = new Vector3(0.030f, 0.035f, 0.070f);
        var twilightColor2 = new Vector3(0.045f, 0.045f, 0.095f);
        var twilightColor3 = new Vector3(0.070f, 0.055f, 0.120f);
        var twilightColor4 = new Vector3(0.110f, 0.075f, 0.145f);
        var twilightColor5 = new Vector3(0.160f, 0.100f, 0.170f);
        var sunrisesetColor = new Vector3(0.220f, 0.125f, 0.195f);
        var goldenHourColor1 = new Vector3(0.320f, 0.160f, 0.220f);
        var goldenHourColor2 = new Vector3(0.440f, 0.220f, 0.250f);
        var goldenHourColor3 = new Vector3(0.580f, 0.320f, 0.280f);
        var goldenHourColor4 = new Vector3(0.700f, 0.440f, 0.320f);
        var goldenHourColor5 = new Vector3(0.780f, 0.580f, 0.380f);
        var dayColor1 = new Vector3(0.840f, 0.700f, 0.600f);
        var dayColor2 = new Vector3(0.890f, 0.820f, 0.760f);
        var dayColor3 = new Vector3(0.940f, 0.920f, 0.900f);
        var dayColor4 = new Vector3(0.980f, 0.990f, 1.000f);
        var noonColor = new Vector3(1.250f, 1.250f, 1.250f);

        (double hour, Vector3 color)[] colors =
        [
            (0.00, midnightColor),
            (sunriseTime - 6 / 3.0, nightColor),
            (sunriseTime - 5 / 3.0, twilightColor1),
            (sunriseTime - 4 / 3.0, twilightColor2),
            (sunriseTime - 3 / 3.0, twilightColor3),
            (sunriseTime - 2 / 3.0, twilightColor4),
            (sunriseTime - 1 / 3.0, twilightColor5),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 1 / 3.0, goldenHourColor1),
            (sunriseTime + 2 / 3.0, goldenHourColor2),
            (sunriseTime + 3 / 3.0, goldenHourColor3),
            (sunriseTime + 4 / 3.0, goldenHourColor4),
            (sunriseTime + 5 / 3.0, goldenHourColor5),
            (sunriseTime + 6 / 3.0, dayColor1),
            (sunriseTime + 7 / 3.0, dayColor2),
            (sunriseTime + 8 / 3.0, dayColor3),
            (sunriseTime + 9 / 3.0, dayColor4),
            (noonTime, noonColor),
            (sunsetTime - 9 / 3.0, dayColor4),
            (sunsetTime - 8 / 3.0, dayColor3),
            (sunsetTime - 7 / 3.0, dayColor2),
            (sunsetTime - 6 / 3.0, dayColor1),
            (sunsetTime - 5 / 3.0, goldenHourColor5),
            (sunsetTime - 4 / 3.0, goldenHourColor4),
            (sunsetTime - 3 / 3.0, goldenHourColor3),
            (sunsetTime - 2 / 3.0, goldenHourColor2),
            (sunsetTime - 1 / 3.0, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 1 / 3.0, twilightColor5),
            (sunsetTime + 2 / 3.0, twilightColor4),
            (sunsetTime + 3 / 3.0, twilightColor3),
            (sunsetTime + 4 / 3.0, twilightColor2),
            (sunsetTime + 5 / 3.0, twilightColor1),
            (sunsetTime + 6 / 3.0, nightColor),
            (24.00, midnightColor),
        ];

        foreach ((var hour, var color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
