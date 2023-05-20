using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;

namespace FancyLighting;

internal interface IFancyLightingEngine
{
    public void Unload();

    public void SetLightMapArea(Rectangle value);

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightDecay,
        int width,
        int height
    );
}
