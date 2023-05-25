using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;

namespace FancyLighting.LightingEngines;

public interface ICustomLightingEngine
{
    public void Unload();

    public void SetLightMapArea(Rectangle value);

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightMasks,
        int width,
        int height
    );
}
