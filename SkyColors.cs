using FancyLighting.Config;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

namespace FancyLighting;

public sealed class SkyColors
{
    public static List<(double time, Vector3 lightColor)> ColorsByTime { get; private set; }

    private static bool _dayTimeTmp;
    private static bool _dontStarveWorldTmp;

    internal static void Initialize()
    {
        double noonTime = 12.0;
        double sunriseTime = noonTime - 7.0;
        double sunsetTime = noonTime + 7.0;

        Vector3 midnightColor = new Vector3(8, 10, 15) / 255f;
        Vector3 twilightColor1 = new Vector3(10, 13, 18) / 255f;
        Vector3 twilightColor2 = new Vector3(80, 35, 23) / 255f;
        Vector3 sunrisesetColor = new Vector3(150, 70, 32) / 255f;
        Vector3 goldenHourColor1 = new Vector3(250, 130, 55) / 255f;
        Vector3 goldenHourColor2 = new Vector3(300, 230, 170) / 255f;
        Vector3 noonColor = new Vector3(360, 460, 560) / 255f;

        ColorsByTime = new()
        {
            (0.0, midnightColor),
            (sunriseTime - 1.5, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.5, goldenHourColor1),
            (sunriseTime + 1.5, goldenHourColor2),
            (noonTime, noonColor),
            (sunsetTime - 1.5, goldenHourColor2),
            (sunsetTime - 0.5, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.5, twilightColor1),
            (24.0, midnightColor)
        };
    }

    internal static void AddSkyColorsHooks()
    {
        Initialize();

        On.Terraria.Main.SetBackColor += _SetBackColor;
        On.Terraria.GameContent.DontStarveSeed.ModifyNightColor += _ModifyNightColor;
    }

    private static void _SetBackColor(
        On.Terraria.Main.orig_SetBackColor orig,
        Main.InfoToSetBackColor info,
        out Color sunColor,
        out Color moonColor
    )
    {
        if (!(LightingConfig.Instance?.CustomSkyColorsEnabled() ?? false))
        {
            orig(info, out sunColor, out moonColor);
            return;
        }

        orig(info, out sunColor, out moonColor);

        _dayTimeTmp = Main.dayTime;
        _dontStarveWorldTmp = Main.dontStarveWorld;
        Main.dayTime = false;
        Main.dontStarveWorld = true;
        // info is a struct, so we don't have to reset this value
        info.isInGameMenuOrIsServer = false;
        try
        {
            orig(info, out _, out _);
        }
        finally
        {
            Main.dayTime = _dayTimeTmp;
            Main.dontStarveWorld = _dontStarveWorldTmp;
        }
    }

    private static void _ModifyNightColor(
        On.Terraria.GameContent.DontStarveSeed.orig_ModifyNightColor orig,
        ref Color backColor,
        ref Color moonColor
    )
    {
        if (!(LightingConfig.Instance?.CustomSkyColorsEnabled() ?? true))
        {
            orig(ref backColor, ref moonColor);
            return;
        }

        Main.dayTime = _dayTimeTmp;
        Main.dontStarveWorld = _dontStarveWorldTmp;
        SetBaseSkyColor(ref backColor);
        if (!Main.dayTime && Main.dontStarveWorld)
        {
            orig(ref backColor, ref moonColor);
        }
    }

    public static void SetBaseSkyColor(ref Color bgColor)
    {
        double hour = Main.dayTime ? 4.5 + (Main.time / 3600.0) : 12.0 + 7.5 + (Main.time / 3600.0);
        VectorToColor.Assign(ref bgColor, 1f, CalculateSkyColor(hour));
    }

    private static (double time, Vector3 lightColor) GetTimeColor(int index)
    {
        if (index < 0)
        {
            (double time, Vector3 lightColor) = ColorsByTime[index + ColorsByTime.Count];
            return (time - 24.0, lightColor);
        }
        if (index >= ColorsByTime.Count)
        {
            (double time, Vector3 lightColor) = ColorsByTime[index - ColorsByTime.Count];
            return (time + 24.0, lightColor);
        }
        return ColorsByTime[index];
    }

    public static Vector3 CalculateSkyColor(double hour)
    {
        hour %= 24.0;

        static Vector3 chooseDerivative(double x1, double x2, double x3, Vector3 y1, Vector3 y2, Vector3 y3)
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

        // Use cubic Hermite spline interpolation
        for (int i = 1; i < ColorsByTime.Count; ++i)
        {
            if (hour > ColorsByTime[i].time)
            {
                continue;
            }

            (double time1, Vector3 color1) = GetTimeColor(i - 2);
            (double time2, Vector3 color2) = GetTimeColor(i - 1);
            (double time3, Vector3 color3) = GetTimeColor(i);
            (double time4, Vector3 color4) = GetTimeColor(i + 1);

            Vector3 y1 = color2;
            Vector3 m1 = chooseDerivative(time1, time2, time3, color1, color2, color3);

            float x2 = (float)(time3 - time2);
            float x2_2 = x2 * x2;
            Vector3 y2 = color3;
            Vector3 m2 = chooseDerivative(time2, time3, time4, color2, color3, color4);

            Vector3 y_diff = y2 - y1;

            Vector3 a = 2 * y_diff - x2 * (m1 + m2);
            Vector3 b = x2_2 * m2 + 2 * x2_2 * m1 - 3 * x2 * y_diff;
            Vector3 c = m1;
            Vector3 d = y1;

            float den = -x2_2 * x2;
            a /= den;
            b /= den;

            float x = (float)(hour - time2);
            return Vector3.Min(d + x * (c + x * (b + x * a)), Vector3.One);
        }

        return ColorsByTime[^1].lightColor;
    }
}
