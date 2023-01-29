using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.ModLoader;

namespace FancyLighting.Util;

internal static class EffectLoader
{
    public static Shader LoadEffect(
        string filePath,
        string passName,
        bool hiDef = false
    )
    {
        Effect effect = ModContent.Request<Effect>(
            filePath, ReLogic.Content.AssetRequestMode.ImmediateLoad
        ).Value;

        string hiDefPassName;
        if (hiDef)
        {
            hiDefPassName = passName + "HiDef";
        }
        else
        {
            hiDefPassName = null;
        }

        return new Shader(effect, passName, hiDefPassName);
    }

    public static void UnloadEffect(ref Shader shader)
    {
        try
        {
            shader.Unload();
        }
        catch (Exception) // Shouldn't normally happen
        {

        }
        finally
        {
            shader = null;
        }
    }
}
