using Microsoft.Xna.Framework;
using System;
using Terraria.Graphics.Light;

namespace FancyLighting;

internal abstract class FancyLightingEngineBase
{
    protected int[][] _circles;
    protected Rectangle _lightMapArea;

    protected void ComputeCircles(int maxLightRange)
    {
        _circles = new int[maxLightRange + 1][];
        _circles[0] = new int[] { 0 };
        for (int radius = 1; radius < maxLightRange + 1; ++radius)
        {
            _circles[radius] = new int[radius + 1];
            _circles[radius][0] = radius;
            double diagonal = radius / Math.Sqrt(2.0);
            for (int x = 1; x <= radius; ++x)
            {
                _circles[radius][x] = x <= diagonal
                    ? (int)Math.Ceiling(Math.Sqrt(radius * radius - x * x))
                    : (int)Math.Floor(Math.Sqrt(radius * radius - (x - 1) * (x - 1)));
            }
        }
    }

    public void SetLightMapArea(Rectangle value) => _lightMapArea = value;

    public abstract void SpreadLight(
        LightMap lightMap,
        Vector3[] colors,
        LightMaskMode[] lightDecay,
        int width,
        int height
    );
}
