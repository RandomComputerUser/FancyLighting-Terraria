using System;
using Terraria.Graphics.Shaders;

namespace FancyLighting.Util
{
    public static class EffectLoader
    {
        public static void UnloadEffect(string effectKey)
        {
            try
            {
                GameShaders.Misc.TryGetValue(effectKey, out MiscShaderData? shader);
                shader?.Shader?.Dispose();
                GameShaders.Misc.Remove(effectKey);
            }
            catch (Exception) // Shouldn't happen and I don't know how this should be handled
            {

            }
        }
    }
}
