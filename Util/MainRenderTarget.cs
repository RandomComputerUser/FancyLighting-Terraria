using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FancyLighting.Util;

internal static class MainRenderTarget
{
    public static RenderTarget2D Get()
    {
        RenderTargetBinding[] renderTargets =
            Main.graphics.GraphicsDevice.GetRenderTargets();
        RenderTarget2D renderTarget =
            renderTargets is null || renderTargets.Length < 1
                ? null
                : (RenderTarget2D)renderTargets[0].RenderTarget;
        return renderTarget;
    }
}
