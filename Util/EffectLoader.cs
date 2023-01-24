using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
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
        MiscShaderData shaderData = new(
            new Ref<Effect>(ModContent.Request<Effect>(
                filePath, ReLogic.Content.AssetRequestMode.ImmediateLoad
            ).Value),
            passName
        );

        MiscShaderData hiDefShaderData = null;
        if (hiDef)
        {
            hiDefShaderData = new(
                new Ref<Effect>(ModContent.Request<Effect>(
                    filePath, ReLogic.Content.AssetRequestMode.ImmediateLoad
                ).Value),
                passName + "HiDef"
            );
        }

        return new Shader(shaderData, hiDefShaderData);
    }

    public static void UnloadEffect(ref Shader effect)
    {
        try
        {
            ((MiscShaderData)effect)?.Shader?.Dispose();
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
