using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace FancyLighting.Util;

public static class TextureMaker
{
    public static bool HiDef
        => Main.instance.GraphicsDevice.GraphicsProfile == GraphicsProfile.HiDef;

    public static SurfaceFormat TextureSurfaceFormat
        => HiDef
            ? SurfaceFormat.Rgba64
            : SurfaceFormat.Color;

    public static void MakeSize(ref RenderTarget2D target, int width, int height)
    {
        if (target is null
            || target.GraphicsDevice != Main.graphics.GraphicsDevice
            || target.Width != width
            || target.Height != height
            || target.Format != TextureSurfaceFormat
        )
        {
            target?.Dispose();
            target = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                TextureSurfaceFormat,
                DepthFormat.None
            );
        }
    }

    public static void MakeAtLeastSize(ref RenderTarget2D target, int width, int height)
    {
        if (target is null
            || target.GraphicsDevice != Main.graphics.GraphicsDevice
            || target.Width < width
            || target.Height < height
            || target.Format != TextureSurfaceFormat
        )
        {
            target?.Dispose();
            width = Math.Max(width, target?.Width ?? 0);
            height = Math.Max(height, target?.Height ?? 0);
            target = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                TextureSurfaceFormat,
                DepthFormat.None
            );
        }
    }

    public static void MakeAtLeastSize(ref Texture2D texture, int width, int height)
    {
        if (texture is null
            || texture.GraphicsDevice != Main.graphics.GraphicsDevice
            || texture.Width < width
            || texture.Height < height
            || texture.Format != TextureSurfaceFormat
        )
        {
            width = Math.Max(width, texture?.Width ?? 0);
            height = Math.Max(height, texture?.Height ?? 0);

            texture?.Dispose();
            texture = new Texture2D(
                Main.graphics.GraphicsDevice,
                width,
                height,
                false,
                TextureSurfaceFormat
            );
        }
    }
}
