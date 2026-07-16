namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure math and geometry utility functions.
/// </summary>
public static class JmoMath
{
    /// <summary>
    /// Maps a value from input range [inMin, inMax] to output range [outMin, outMax].
    /// Supports extrapolation (values outside input range map beyond output range).
    /// Returns <paramref name="outMin"/> when <paramref name="inMin"/> equals <paramref name="inMax"/>
    /// to avoid division by zero.
    /// </summary>
    public static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        if (inMax == inMin) { return outMin; }
        return outMin + (outMax - outMin) * ((value - inMin) / (inMax - inMin));
    }

    /// <summary>
    /// Evaluates a quadratic Bezier curve at parameter <paramref name="t"/> (0 to 1).
    /// P0 is the start, P1 is the control point, P2 is the end.
    /// </summary>
    public static Vector3 QuadraticBezier3D(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        Vector3 q0 = p0.Lerp(p1, t);
        Vector3 q1 = p1.Lerp(p2, t);
        Vector3 r = q0.Lerp(q1, t);
        return r;
    }

    /// <summary>
    /// Returns a point on a ring (circle) around a center point, oriented by the given basis.
    /// Useful for aim rings, orbital positioning, melee attack targeting, etc.
    /// </summary>
    /// <param name="center">The center point of the ring (e.g., pivot GlobalPosition)</param>
    /// <param name="basis">The orientation of the ring plane (e.g., pivot GlobalTransform.Basis)</param>
    /// <param name="direction">2D direction input mapped to XZ plane</param>
    /// <param name="radius">Distance from center to ring edge</param>
    /// <param name="defaultDirection">Fallback direction when input is zero (defaults to Vector2.Right)</param>
    public static Vector3 GetPointOnRing(
        Vector3 center,
        Basis basis,
        Vector2 direction,
        float radius,
        Vector2? defaultDirection = null)
    {
        var dir = direction.IsZeroApprox()
            ? (defaultDirection ?? Vector2.Right)
            : direction.Normalized();

        return center + (basis * new Vector3(dir.X, 0, dir.Y) * radius);
    }

    /// <summary>Returns all values of enum type <typeparamref name="T"/> in declaration order.</summary>
    public static IEnumerable<T> GetEnumValues<T>() where T : struct, Enum
    {
        return Enum.GetValues<T>();
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing.
    /// Smoothly moves <paramref name="current"/> toward <paramref name="target"/> at a rate
    /// controlled by <paramref name="speed"/>. Higher speed = faster convergence.
    /// Mathematically: Lerp(current, target, 1 - exp(-speed * delta)).
    /// </summary>
    /// <param name="current">Current value.</param>
    /// <param name="target">Target value to approach.</param>
    /// <param name="speed">Decay rate (1/s). Higher = faster convergence. 0 = no movement.</param>
    /// <param name="delta">Frame delta time in seconds.</param>
    public static float ExpDecay(float current, float target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * delta));
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing for Vector2.
    /// </summary>
    public static Vector2 ExpDecay(Vector2 current, Vector2 target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        float t = 1f - Mathf.Exp(-speed * delta);
        return current.Lerp(target, t);
    }

    /// <summary>
    /// Frame-rate-independent exponential decay smoothing for Vector3.
    /// </summary>
    public static Vector3 ExpDecay(Vector3 current, Vector3 target, float speed, float delta)
    {
        if (delta <= 0f) { return current; }
        if (speed <= 0f) { return current; }
        float t = 1f - Mathf.Exp(-speed * delta);
        return current.Lerp(target, t);
    }

    /// <summary>
    /// Returns the index of the nearest candidate to <paramref name="origin"/>,
    /// or <c>-1</c> if the list is empty. Ties broken by list order (first wins).
    /// </summary>
    /// <remarks>
    /// Takes positions rather than nodes deliberately: <c>Node3D.GlobalPosition</c> returns
    /// <c>Vector3.Zero</c> outside a live SceneTree, so a node-based signature would force every
    /// caller's tests into a scene fixture. Callers extract positions at the call site instead,
    /// keeping this helper a pure function.
    /// </remarks>
    public static int SelectNearest(IReadOnlyList<Vector3> candidatePositions, Vector3 origin)
    {
        if (candidatePositions.Count == 0) { return -1; }

        int bestIdx = 0;
        float bestDistSq = origin.DistanceSquaredTo(candidatePositions[0]);

        for (int i = 1; i < candidatePositions.Count; i++)
        {
            float distSq = origin.DistanceSquaredTo(candidatePositions[i]);
            if (distSq < bestDistSq)
            {
                bestIdx = i;
                bestDistSq = distSq;
            }
        }

        return bestIdx;
    }

    /// <summary>
    /// Closest distance from point <paramref name="p"/> to the line segment [<paramref name="a"/>, <paramref name="b"/>].
    /// Projects <paramref name="p"/> onto the segment and clamps the parameter to [0,1] so points
    /// beyond the endpoints measure to the nearest endpoint. Degenerate segments (a == b) collapse
    /// to point-to-point distance.
    /// </summary>
    public static float ClosestDistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < 1e-6f) { return p.DistanceTo(a); }
        float t = (p - a).Dot(ab) / lenSq;
        t = Mathf.Clamp(t, 0f, 1f);
        var closest = a + ab * t;
        return p.DistanceTo(closest);
    }

    /// <summary>
    /// In-plane facing angle (local-Y rotation, radians) for single-direction art living under
    /// the project's iso-tilted sprite basis. Projects <paramref name="worldDir"/> to the screen
    /// plane — horizontal = X, depth = Z, optionally folding vertical velocity into depth so a
    /// gravity-arcing body noses down under the iso camera. Returns <c>null</c> when the projected
    /// direction is ~zero, signalling the caller to leave the current rotation unchanged.
    /// This is the single source for the convention formerly hand-rolled across every facing site.
    /// </summary>
    /// <param name="worldDir">World-space direction (need not be normalized).</param>
    /// <param name="artBaseAngleOffsetDegrees">Degrees added when the art's neutral heading isn't +X.</param>
    /// <param name="includeVertical">Fold the vertical (Y) component into the depth axis (gravity dip).</param>
    /// <param name="depthForeshorten">
    /// Screen foreshortening of the depth axis relative to horizontal (= sin(camera pitch)); the depth
    /// term is scaled by it so the facing matches the on-screen travel angle under a tilted ortho camera.
    /// <c>1f</c> = no foreshortening (top-down). Supply <c>VisualProjectionDefaults.DepthForeshorten</c>.
    /// </param>
    public static float? IsoPlaneFacingAngle(Vector3 worldDir, float artBaseAngleOffsetDegrees,
        bool includeVertical, float depthForeshorten = 1f)
    {
        float xl = worldDir.X;
        float depth = worldDir.Z - (includeVertical ? worldDir.Y : 0f);
        if (Mathf.IsZeroApprox(xl) && Mathf.IsZeroApprox(depth)) { return null; }
        return Mathf.Atan2(-depth * depthForeshorten, xl) + Mathf.DegToRad(artBaseAngleOffsetDegrees);
    }

    /// <summary>
    /// In-plane facing angle (local-Y rotation, radians) for a planar aim vector — X horizontal,
    /// Y screen-depth (e.g. a right-stick aim). Equivalent to the <see cref="IsoPlaneFacingAngle(Vector3, float, bool)"/>
    /// world form with the aim lifted to (X, 0, Y) and no vertical fold. Returns <c>null</c> on a zero aim.
    /// </summary>
    public static float? IsoPlaneFacingAngle(Vector2 planarAim, float artBaseAngleOffsetDegrees,
        float depthForeshorten = 1f)
    {
        if (Mathf.IsZeroApprox(planarAim.X) && Mathf.IsZeroApprox(planarAim.Y)) { return null; }
        return Mathf.Atan2(-planarAim.Y * depthForeshorten, planarAim.X)
            + Mathf.DegToRad(artBaseAngleOffsetDegrees);
    }

    /// <summary>
    /// Single source of truth for the horizontal-mirror convention shared by the wizard's
    /// <c>FacingFlipController</c> and the spell facing stack: whether single-direction art authored
    /// facing <paramref name="artFacesRight"/> should be H-mirrored to match an aim whose horizontal
    /// (screen-X) component is <paramref name="dirX"/>. Returns <c>null</c> when the aim is ~pure-vertical
    /// (|dirX| &lt; <paramref name="epsilon"/>), signalling the caller to HOLD its current mirror rather
    /// than thrash it straight up/down. Orthogonal to <see cref="MirroredInPlaneRotation"/>: decide the
    /// mirror here, then ask that for the matching rotation.
    /// </summary>
    public static bool? ShouldMirrorHorizontal(float dirX, bool artFacesRight, float epsilon = 0.01f)
        => Mathf.Abs(dirX) < epsilon ? null : (dirX < 0f) == artFacesRight;

    /// <summary>
    /// In-plane rotation (radians) for single-direction art that rotates to face travel while being
    /// H-mirrored. When <paramref name="flip"/> is false this is <paramref name="baseAngle"/> unchanged;
    /// when true it is <c>baseAngle − π</c>, because mirroring negates the sprite's local X and
    /// <c>reflect∘rotate(baseAngle − π)</c> lands the nose back on the travel direction with the art's
    /// top kept in the upper screen half. The <paramref name="flip"/> dependency is geometric, not a
    /// coupling smell — the rotation and the mirror are applied to independent sprite channels (node
    /// basis vs FlipH); only the rotation VALUE must account for the mirror.
    /// </summary>
    public static float MirroredInPlaneRotation(float baseAngle, bool flip)
        => flip ? baseAngle - Mathf.Pi : baseAngle;
}
