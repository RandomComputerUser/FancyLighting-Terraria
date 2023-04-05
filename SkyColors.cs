using FancyLighting.Config;
using FancyLighting.Profiles;
using FancyLighting.Profiles.SkyColor;
using FancyLighting.Util;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;

namespace FancyLighting;

public static class SkyColors
{
    public static Dictionary<SkyColorPreset, ISimpleColorProfile> Profiles { get; private set; }

    private static bool _dayTimeTmp;
    private static bool _dontStarveWorldTmp;

    internal static void Initialize() => Profiles = new()
    {
        [SkyColorPreset.Profile1] = new SkyColors1(),
        [SkyColorPreset.Profile2] = new SkyColors2(),
        [SkyColorPreset.Profile3] = new SkyColors3()
    };

    internal static void AddSkyColorsHooks()
    {
        Initialize();

        Terraria.On_Main.SetBackColor += _SetBackColor;
        Terraria.GameContent.On_DontStarveSeed.ModifyNightColor += _ModifyNightColor;
    }

    private static void _SetBackColor(
        Terraria.On_Main.orig_SetBackColor orig,
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
        Terraria.GameContent.On_DontStarveSeed.orig_ModifyNightColor orig,
        ref Color backColor,
        ref Color moonColor
    )
    {
        if (Profiles is null || !(LightingConfig.Instance?.CustomSkyColorsEnabled() ?? false))
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

    public static Vector3 CalculateSkyColor(double hour)
    {
        bool foundProfile = Profiles.TryGetValue(
            LightingConfig.Instance.CustomSkyPreset,
            out ISimpleColorProfile profile
        );

        if (!foundProfile)
        {
            return Vector3.One;
        }

        return profile.GetColor(hour);
    }
}
