using Microsoft.Xna.Framework;
using Terraria.Graphics.Shaders;

namespace FancyLighting.Util;

internal class Shader
{
    private MiscShaderData _shaderData;
    private MiscShaderData _hiDefShaderData;

    internal Shader(MiscShaderData shaderData, MiscShaderData hiDefShaderData = null)
    {
        _shaderData = shaderData;
        _hiDefShaderData = hiDefShaderData;
    }

    public static implicit operator MiscShaderData(Shader shader)
        => shader._hiDefShaderData is not null && FancyLightingMod.HiDefFeaturesEnabled
            ? shader._hiDefShaderData
            : shader._shaderData;

    internal MiscShaderData UseShaderSpecificData(Vector4 vec)
        => ((MiscShaderData)this)?.UseShaderSpecificData(vec);

    internal void Apply() => ((MiscShaderData)this)?.Apply();
}
