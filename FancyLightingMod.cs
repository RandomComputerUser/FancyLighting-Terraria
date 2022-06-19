using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FancyLighting
{
	public class FancyLightingMod : Mod
	{
		internal static BlendState MultiplyBlend;

		internal static bool _smoothLightingEnabled;
		internal static bool _ambientOcclusionEnabled;
		internal static bool _overrideLightingColor;

		internal SmoothLighting SmoothLightingObj;
		internal AmbientOcclusion AmbientOcclusionObj;

		public static bool SmoothLightingEnabled
		{
			get
			{
				return _smoothLightingEnabled;
			}
		}

		public static bool AmbientOcclusionEnabled
        {
			get
            {
				return _ambientOcclusionEnabled;
            }
        }

		public static bool OverrideLightingColor
        {
			get
            {
				return _smoothLightingEnabled && _overrideLightingColor;
            }
		}

		public override void Load()
		{
			if (Main.netMode == NetmodeID.Server) return;

			_overrideLightingColor = false;

			FancyLightingModSystem.UpdateSettings();

			BlendState blend = new BlendState();
			blend.ColorBlendFunction = BlendFunction.Add;
			blend.ColorSourceBlend = Blend.Zero;
			blend.ColorDestinationBlend = Blend.SourceColor;
			MultiplyBlend = blend;

			SmoothLightingObj = new SmoothLighting();
			AmbientOcclusionObj = new AmbientOcclusion();

			On.Terraria.Graphics.Light.LightingEngine.GetColor +=
			(
				On.Terraria.Graphics.Light.LightingEngine.orig_GetColor orig,
				Terraria.Graphics.Light.LightingEngine self,
				int x,
				int y
			) =>
			{
				if (OverrideLightingColor)
                {
					Vector3 color = orig(self, x, y);
					if (color.X <= 1 / 255f && color.Y <= 1 / 255f && color.Y <= 1 / 255f)
                    {
						color.X = 1 / 255f;
						color.Y = 1 / 255f;
						color.Z = 1 / 255f;
						return color;
					}
					return Vector3.One;
				}
				return orig(self, x, y);
			};

			On.Terraria.Main.RenderTiles +=
			(
				On.Terraria.Main.orig_RenderTiles orig,
				Terraria.Main self
			) =>
			{
				_overrideLightingColor = true;
				orig(self);
				_overrideLightingColor = false;
				if (Main.drawToScreen)
					return;
				SmoothLightingObj.CalculateSmoothLighting(false);
				SmoothLightingObj.DrawSmoothLightingTiles();
			};

			On.Terraria.Main.RenderWalls +=
			(
				On.Terraria.Main.orig_RenderWalls orig,
				Terraria.Main self
			) =>
			{
				_overrideLightingColor = true;
				orig(self);
				_overrideLightingColor = false;
				if (Main.drawToScreen)
					return;
				SmoothLightingObj.CalculateSmoothLighting(true);
				SmoothLightingObj.DrawSmoothLightingWalls();
				AmbientOcclusionObj.ApplyAmbientOcclusion();
			};

			On.Terraria.Main.RenderTiles2 +=
			(
				On.Terraria.Main.orig_RenderTiles2 orig,
				Terraria.Main self
			) =>
			{
				_overrideLightingColor = true;
				orig(self);
				_overrideLightingColor = false;
				if (Main.drawToScreen)
					return;
				SmoothLightingObj.CalculateSmoothLighting(false);
				SmoothLightingObj.DrawSmoothLightingTiles2();
			};

		}

    }
}