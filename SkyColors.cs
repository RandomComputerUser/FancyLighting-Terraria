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

        Vector3 midnightColor = new(5, 10, 15);
        Vector3 twilightColor1 = new(10, 15, 20);
        Vector3 twilightColor2 = new(80, 35, 30);
        Vector3 sunrisesetColor = new(150, 70, 40);
        Vector3 goldenHourColor1 = new(250, 130, 55);
        Vector3 goldenHourColor2 = new(300, 230, 170);
        Vector3 noonColor = new(360, 380, 400);

        ColorsByTime = new()
        {
            (0.0, midnightColor),
            (sunriseTime - 1.4, twilightColor1),
            (sunriseTime - 0.5, twilightColor2),
            (sunriseTime, sunrisesetColor),
            (sunriseTime + 0.4, goldenHourColor1),
            (sunriseTime + 1.2, goldenHourColor2),
            (noonTime, noonColor),
            (sunsetTime - 1.2, goldenHourColor2),
            (sunsetTime - 0.4, goldenHourColor1),
            (sunsetTime, sunrisesetColor),
            (sunsetTime + 0.5, twilightColor2),
            (sunsetTime + 1.4, twilightColor1),
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
        hour %= 24.0;

        static Color lerp(double lerpHour, double startHour, double endHour, Vector3 startColor, Vector3 endColor)
        {
            float t = (float)((lerpHour - startHour) / (endHour - startHour));
            float r = ((1 - t) * startColor.X) + (t * endColor.X);
            float g = ((1 - t) * startColor.Y) + (t * endColor.Y);
            float b = ((1 - t) * startColor.Z) + (t * endColor.Z);

            Color result = Color.White;
            ColorConverter.Assign(ref result, 1f, new Vector3(r / 255f, g / 255f, b / 255f));
            return result;
        }

        for (int i = 1; i < ColorsByTime.Count; ++i)
        {
            if (hour <= ColorsByTime[i].time)
            {
                bgColor = lerp(
                    hour,
                    ColorsByTime[i - 1].time,
                    ColorsByTime[i].time,
                    ColorsByTime[i - 1].lightColor,
                    ColorsByTime[i].lightColor
                );
                return;
            }
        }

        ColorConverter.Assign(ref bgColor, 1f, ColorsByTime[^1].lightColor);
    }
}
