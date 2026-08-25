namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Resolves where a body's units stand the moment it spawns: each one spacing behind the unit ahead
/// of it, on ground that unit could have walked from.
/// </summary>
/// <remarks>
/// <para>
/// A straight-back placement extends several metres of body from a head that may itself have been
/// placed on the last metre of its platform, so the tail lands over void or inside geometry with
/// nothing to report it. Here every step is probed: a step that finds no ground, or ground more than
/// one spacing above or below the unit ahead of it, is refused and the walk yaws until it finds one.
/// A step with no legal direction left stacks the unit on its predecessor — a body compresses rather
/// than reaching into the void.
/// </para>
/// <para>
/// The probe answers "what Y would a unit standing here have", height above the surface included, so
/// this walk holds no notion of a collider and no tunable of its own. A probe that misses under the
/// HEAD is read as "this body is not standing on ground at all" — a flier, or a scene with no floor —
/// and the whole chain is laid straight back at the head's own pose, which is the only layout that
/// carries no claim about ground.
/// </para>
/// </remarks>
internal static class ChainLayout
{
    /// <summary>
    /// Answers the Y a unit placed at <paramref name="point"/> would stand at, and false when nothing
    /// lies under it. Only X and Z of <paramref name="point"/> are a request; its Y is a hint about
    /// where to look from.
    /// </summary>
    internal delegate bool GroundProbe(Vector3 point, out float standingY);

    // Tried in order per step, so a body prefers to keep going straight and yaws only as far as it
    // must; the wide tail is what lets it double back along a ledge instead of stacking on itself.
    private static readonly float[] CurlYawDegrees =
    {
        0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f, 125f, -125f, 150f, -150f, 180f,
    };

    /// <summary>
    /// The poses for a head plus <paramref name="count"/> units, front first: index 0 is the head
    /// itself, so the result seeds a <see cref="PositionHistory"/> as it stands.
    /// </summary>
    /// <param name="headFacing">The head's travel direction. The body is laid out opposite it.</param>
    /// <param name="spacing">Metres between unit centres, and the largest ground step a unit may take.</param>
    internal static List<(Vector3 position, Vector3 facing)> Resolve(
        Vector3 headPosition, Vector3 headFacing, int count, float spacing, GroundProbe probe)
    {
        var forward = headFacing.IsZeroApprox() ? Vector3.Forward : headFacing.Normalized();
        var poses = new List<(Vector3 position, Vector3 facing)>(Math.Max(count, 0) + 1)
        {
            (headPosition, forward),
        };

        if (count <= 0) { return poses; }

        if (!probe(headPosition, out _))
        {
            for (var k = 0; k < count; k++)
            {
                poses.Add((poses[k].position - (forward * spacing), forward));
            }

            return poses;
        }

        // Kept flat: a step is a move across the floor, and the probe supplies the height it lands at.
        var step = new Vector3(-forward.X, 0f, -forward.Z);
        step = step.IsZeroApprox() ? Vector3.Back : step.Normalized();

        for (var k = 0; k < count; k++)
        {
            var previous = poses[k];
            var placed = false;

            foreach (var yaw in CurlYawDegrees)
            {
                var direction = step.Rotated(Vector3.Up, Mathf.DegToRad(yaw));
                var candidate = previous.position + (direction * spacing);
                if (!probe(candidate, out var standingY)) { continue; }
                if (Mathf.Abs(standingY - previous.position.Y) > spacing) { continue; }

                poses.Add((new Vector3(candidate.X, standingY, candidate.Z), -direction));
                step = direction;
                placed = true;
                break;
            }

            if (!placed) { poses.Add(previous); }
        }

        return poses;
    }
}
