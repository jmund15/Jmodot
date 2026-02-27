namespace Jmodot.Implementation.AI.Navigation.Zones;

using Core.AI.Navigation.Zones;
using Godot;
using Shared;

/// <summary>
/// Spherical zone shape. Containment is based on XZ-plane distance from center.
/// Simplest and most common zone type — suitable for open-area wandering.
/// </summary>
[GlobalClass, Tool]
public partial class SphereZoneShape3D : ZoneShape3D
{
    [Export(PropertyHint.Range, "1.0, 100.0, 0.5")]
    private float _radius = 10f;

    public override float GetNormalizedDistance(Vector3 agentPosition, Vector3 zoneCenter)
    {
        if (_radius <= 0f)
        {
            return 0f;
        }

        Vector3 offset = agentPosition - zoneCenter;
        offset.Y = 0;
        return offset.Length() / _radius;
    }

    public override Vector3 GetDirectionToInterior(Vector3 agentPosition, Vector3 zoneCenter)
    {
        Vector3 toCenter = zoneCenter - agentPosition;
        toCenter.Y = 0;
        return toCenter.LengthSquared() < 0.001f ? Vector3.Forward : toCenter.Normalized();
    }

    public override Vector3 SampleRandomInteriorPoint(Vector3 center)
    {
        if (_radius <= 0f)
        {
            return center;
        }

        Vector3 dir = JmoRng.GetRndVector3ZeroY();
        float dist = Mathf.Sqrt(JmoRng.GetRndFloat()) * _radius;
        return center + dir * dist;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRadius(float value) => _radius = value;
#endif
    #endregion
}
