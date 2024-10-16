using Microsoft.Xna.Framework;

namespace FancyLighting.Profiles.SkyColor;

public class SkyColors2 : ISimpleColorProfile
{
    private SkyColorProfile _profile;

    public SkyColors2()
    {
        // 0.6.0 sky colors

        _profile = new(InterpolationMode.Cubic);

        var noonTime = 12.0;
        var sunriseTime = noonTime - 7.0;
        var sunsetTime = noonTime + 7.0;

        Vector3 midnightColor = new Vector3(8, 10, 15) / 255f;
        Vector3 twilightColor1 = new Vector3(10, 13, 18) / 255f;
        Vector3 twilightColor2 = new Vector3(60, 35, 23) / 255f;
        Vector3 sunrisesetColor = new Vector3(150, 70, 32) / 255f;
        Vector3 goldenHourColor1 = new Vector3(250, 130, 55) / 255f;
        Vector3 goldenHourColor2 = new Vector3(300, 230, 170) / 255f;
        Vector3 noonColor = new Vector3(360, 460, 560) / 255f;

        (double hour, Vector3 color)[] colors = new[]
        {
            (0.0, midnightColor),
            (sunriseTime - 1.5, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.75, goldenHourColor1),
            (sunriseTime + 1.5, goldenHourColor2),
            (noonTime, noonColor),
            (sunsetTime - 1.5, goldenHourColor2),
            (sunsetTime - 0.75, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.5, twilightColor1),
            (24.0, midnightColor),
        };

        foreach ((var hour, var color) in colors)
        {
            _profile.AddColor(hour, color);
        }
    }

    public Vector3 GetColor(double hour) => _profile.GetColor(hour);
}
