using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace FancyLighting
{
    class SkyColors
    {
        public static double MidnightTime { get; set; }
        public static double NoonTime { get; set; }
        public static double DayEndTime { get; set; }
        public static double SunriseTime { get; set; }
        public static double SunsetTime { get; set; }
        public static double PreSunriseTime { get; set; }
        public static double PostSunriseTimeEarly { get; set; }
        public static double PostSunriseTimeLate { get; set; }
        public static double PreSunsetTimeEarly { get; set; }
        public static double PreSunsetTimeLate { get; set; }
        public static double PostSunsetTime { get; set; }

        public static Vector3 MidnightColor { get; set; }
        public static Vector3 NoonColor { get; set; }
        public static Vector3 DayNightTransitionColor { get; set; }
        public static Vector3 NightTransitionColor { get; set; }
        public static Vector3 DayTransitionColor { get; set; }
        public static Vector3 DayTransitionColor2 { get; set; }

        public static float BloodMoonFactor { get; set; }

        internal static bool _dayTimeTmp;
        internal static bool _dontStarveWorldTmp;
        internal static bool _bloodMoonActive;

        internal static void Initialize()
        {
            MidnightTime = 0.0;
            NoonTime = 12.0;
            DayEndTime = 24.0;
            SunriseTime = 5.0;
            PreSunriseTime = SunriseTime - 0.5;
            PostSunriseTimeEarly = SunriseTime + 0.5;
            PostSunriseTimeLate = SunriseTime + 1.0;
            SunsetTime = 12.0 + 7.0;
            PreSunsetTimeEarly = SunsetTime - 1.0;
            PreSunsetTimeLate = SunsetTime - 0.5;
            PostSunsetTime = SunsetTime + 0.5;

            MidnightColor = new Vector3(10, 10, 20);
            NightTransitionColor = new Vector3(45, 30, 40);
            DayNightTransitionColor = new Vector3(150, 60, 70);
            DayTransitionColor = new Vector3(200, 120, 90);
            DayTransitionColor2 = new Vector3(230, 180, 140);
            NoonColor = new Vector3(360, 360, 360);

            BloodMoonFactor = 0.5f;
        }

        public static void SetBaseSkyColor(ref Color bgColor)
        {
            double hour;
            if (Main.dayTime)
            {
                hour = 4.5 + Main.time / 3600.0;
            }
            else
            {
                hour = 12.0 + 7.5 + Main.time / 3600.0;
            }
            hour %= 24.0;

            Color lerp(double lerpHour, double startHour, double endHour, Vector3 startColor, Vector3 endColor)
            {
                float t = (float)((lerpHour - startHour) / (endHour - startHour));
                float r = (1 - t) * startColor.X + t * endColor.X;
                float g = (1 - t) * startColor.Y + t * endColor.Y;
                float b = (1 - t) * startColor.Z + t * endColor.Z;

                if (_bloodMoonActive)
                {
                    r *= (1f + BloodMoonFactor);
                    g *= (1f - BloodMoonFactor);
                    b *= (1f - BloodMoonFactor);
                }

                return new Color(
                    (int)Math.Round(r),
                    (int)Math.Round(g),
                    (int)Math.Round(b),
                    255
                );
            }

            // Can't use switch because not constant
            if (hour <= PreSunriseTime)
            {
                bgColor = lerp(hour, MidnightTime, PreSunriseTime, MidnightColor, NightTransitionColor);
            }
            else if (hour <= SunriseTime)
            {
                bgColor = lerp(hour, PreSunriseTime, SunriseTime, NightTransitionColor, DayNightTransitionColor);
            }
            else if (hour <= PostSunriseTimeEarly)
            {
                bgColor = lerp(hour, SunriseTime, PostSunriseTimeEarly, DayNightTransitionColor, DayTransitionColor);
            }
            else if (hour <= PostSunriseTimeLate)
            {
                bgColor = lerp(hour, PostSunriseTimeEarly, PostSunriseTimeLate, DayTransitionColor, DayTransitionColor2);
            }
            else if (hour <= NoonTime)
            {
                bgColor = lerp(hour, PostSunriseTimeLate, NoonTime, DayTransitionColor2, NoonColor);
            }
            else if (hour <= PreSunsetTimeEarly)
            {
                bgColor = lerp(hour, NoonTime, PreSunsetTimeEarly, NoonColor, DayTransitionColor2);
            }
            else if (hour <= PreSunsetTimeLate)
            {
                bgColor = lerp(hour, PreSunsetTimeEarly, PreSunsetTimeLate, DayTransitionColor2, DayTransitionColor);
            }
            else if (hour <= SunsetTime)
            {
                bgColor = lerp(hour, PreSunsetTimeLate, SunsetTime, DayTransitionColor, DayNightTransitionColor);
            }
            else if (hour <= PostSunsetTime)
            {
                bgColor = lerp(hour, SunsetTime, PostSunsetTime, DayNightTransitionColor, NightTransitionColor);
            }
            else
            {
                bgColor = lerp(hour, PostSunsetTime, DayEndTime, NightTransitionColor, MidnightColor);
            }
        }

        internal static void AddSkyColorsHooks()
        {
            Initialize();

            On.Terraria.Main.SetBackColor +=
            (
                On.Terraria.Main.orig_SetBackColor orig,
                Main.InfoToSetBackColor info,
                out Color sunColor,
                out Color moonColor
            ) =>
            {
                if (!FancyLightingMod.CustomSkyColorsEnabled)
                {
                    orig(info, out sunColor, out moonColor);
                    return;
                }

                info.isInGameMenuOrIsServer = false;
                orig(info, out Color sunColorTmp, out Color moonColorTmp);

                _dayTimeTmp = Main.dayTime;
                _dontStarveWorldTmp = Main.dontStarveWorld;
                _bloodMoonActive = info.BloodMoonActive; // && !Main.dayTime;
                Main.dayTime = false;
                Main.dontStarveWorld = true;
                orig(info, out sunColor, out moonColor);
                Main.dayTime = _dayTimeTmp;
                Main.dontStarveWorld = _dontStarveWorldTmp;

                sunColor = sunColorTmp;
                moonColor = moonColorTmp;
            };

            On.Terraria.GameContent.DontStarveSeed.ModifyNightColor +=
            (
                On.Terraria.GameContent.DontStarveSeed.orig_ModifyNightColor orig,
                ref Color backColor,
                ref Color moonColor
            ) =>
            {
                if (!FancyLightingMod.CustomSkyColorsEnabled)
                {
                    orig(ref backColor, ref moonColor);
                    return;
                }

                Main.dayTime = _dayTimeTmp;
                Main.dontStarveWorld = _dontStarveWorldTmp;
                SetBaseSkyColor(ref backColor);
                if (!Main.dayTime && Main.dontStarveWorld)
                    orig(ref backColor, ref moonColor);
            };
        }
    }
}
