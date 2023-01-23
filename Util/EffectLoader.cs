using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace FancyLighting.Util;

public static class EffectLoader
{
    public static MiscShaderData LoadEffect(
        string filePath,
        string passName,
        bool hiDef = false
    )
    {
        if (hiDef && TextureMaker.HiDef)
        {
            passName += "HiDef";
        }

        return new MiscShaderData(
            new Ref<Effect>(ModContent.Request<Effect>(
                filePath, ReLogic.Content.AssetRequestMode.ImmediateLoad
            ).Value),
            passName
        );
    }

    public static void UnloadEffect(ref MiscShaderData effect)
    {
        try
        {
            effect?.Shader?.Dispose();
        }
        catch (Exception) // Shouldn't normally happen
        {

        }
        finally
        {
            effect = null;
        }
    }
}
