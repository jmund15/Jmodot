namespace Jmodot.Implementation.AI.Navigation.Considerations;

using Godot;

/// <summary>
/// Per-member band responses for goals that close to, hold, or escape a distance. The engine
/// supplies the displacement to the member; these return the direction that member pushes.
/// </summary>
public static class BandScoring
{
    private const float CoLocatedSquared = 0.0001f;

    /// <summary>Toward a member beyond <paramref name="max"/>, away from one inside
    /// <paramref name="min"/>, zero inside the inclusive band or when co-located.</summary>
    public static Vector3 Response(Vector3 toMember, float distance, float min, float max)
    {
        if (!float.IsFinite(distance) || !float.IsFinite(min) || !float.IsFinite(max)) { return Vector3.Zero; }
        if (distance > max) { return Toward(toMember); }
        if (distance < min) { return Away(toMember); }
        return Vector3.Zero;
    }

    /// <summary>The unit direction from the member toward the agent; zero when co-located.</summary>
    public static Vector3 Away(Vector3 toMember)
        => toMember.LengthSquared() < CoLocatedSquared ? Vector3.Zero : -toMember.Normalized();

    private static Vector3 Toward(Vector3 toMember)
        => toMember.LengthSquared() < CoLocatedSquared ? Vector3.Zero : toMember.Normalized();
}
