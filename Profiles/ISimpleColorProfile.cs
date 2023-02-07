using Microsoft.Xna.Framework;

namespace FancyLighting.Profiles;

public interface ISimpleColorProfile
{
    public Vector3 GetColor(double t);
}
