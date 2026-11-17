namespace Jmodot.Implementation.AI.Navigation.Considerations;

using System;
using System.Collections.Generic;
using Core.Movement;
using Godot;
using Jmodot.Implementation.AI.Navigation.Targets;

/// <summary>
/// Non-authored knobs for a response over a target sample. A goal passes the defaults the
/// consideration contract uses (confidence on, no projection, no approach weighting, flat); a
/// consideration supplies its own authored values.
/// </summary>
public readonly struct TargetResponseSettings
{
    public readonly bool ConfidenceWeighted;
    public readonly float ProjectionInfluence;
    public readonly float ApproachSpeedWeight;
    public readonly bool HasVerticalMovement;

    public TargetResponseSettings(bool confidenceWeighted, float projectionInfluence,
        float approachSpeedWeight, bool hasVerticalMovement)
    {
        ConfidenceWeighted = confidenceWeighted;
        ProjectionInfluence = projectionInfluence;
        ApproachSpeedWeight = approachSpeedWeight;
        HasVerticalMovement = hasVerticalMovement;
    }
}

/// <summary>One member's computed response direction and raw weight for a single evaluation.</summary>
public readonly struct TargetResponse
{
    public readonly Vector3 Direction;
    public readonly float Weight;

    public TargetResponse(Vector3 direction, float weight)
    {
        Direction = direction;
        Weight = weight;
    }
}

/// <summary>
/// The one owner of "combine per-member responses into directional scores": projection, member
/// weight, focus, resolution blending and the weighted average. Consumers supply a response
/// per member; they never instantiate another consideration to get the math.
/// </summary>
public static class TargetResponseScoring3D
{
    private const float Epsilon = 0.001f;

    /// <summary>
    /// Computes a per-bin score map from <paramref name="sample"/>'s members.
    /// <paramref name="response"/> receives the member, the flat-or-3D displacement to it and its
    /// length, and returns the direction that member pushes. <paramref name="scratch"/> belongs to
    /// the caller's per-agent runtime and is cleared here, so no per-tick list is allocated.
    /// <para>
    /// A member on the agent contributes no response and no weight. A member whose callback
    /// returns zero still contributes its weight, so one satisfied member can dilute another.
    /// The result is never renormalized against crowd size.
    /// </para>
    /// </summary>
    public static Dictionary<Vector3, float> Aggregate(DirectionSet3D directions, TargetSample sample,
        Vector3 agentPosition, in TargetResponseSettings settings,
        Func<TargetMember, Vector3, float, Vector3> response, List<TargetResponse> scratch)
    {
        scratch.Clear();
        float maxWeight = 0f;

        foreach (var member in sample.Members)
        {
            var effective = EffectivePosition(member, settings.ProjectionInfluence);
            var toTarget = effective - agentPosition;
            if (!settings.HasVerticalMovement) { toTarget.Y = 0f; }
            if (toTarget.LengthSquared() < Epsilon * Epsilon) { continue; }

            var toTargetDirection = toTarget.Normalized();
            var weight = MemberWeight(member, toTargetDirection, settings);
            if (weight < Epsilon) { continue; }

            maxWeight = Mathf.Max(maxWeight, weight);
            scratch.Add(new TargetResponse(response(member, toTarget, toTarget.Length()), weight));
        }

        if (scratch.Count == 0) { return DirectionScoring.ZeroScores(directions); }

        float focus = sample.Focus;
        float resolution = sample.Resolution;

        var combinedDirection = Vector3.Zero;
        float totalFocusedWeight = 0f;
        foreach (var entry in scratch)
        {
            float focusedWeight = FocusWeight(entry.Weight, maxWeight, focus);
            combinedDirection += entry.Direction * focusedWeight;
            totalFocusedWeight += focusedWeight;
        }

        if (totalFocusedWeight < Epsilon) { return DirectionScoring.ZeroScores(directions); }

        var combineFirst = DirectionScoring.ByAlignment(
            directions, combinedDirection / totalFocusedWeight, raw: settings.HasVerticalMovement);

        if (resolution < Epsilon) { return combineFirst; }

        var perTargetScores = DirectionScoring.ZeroScores(directions);
        foreach (var entry in scratch)
        {
            float normalizedWeight = FocusWeight(entry.Weight, maxWeight, focus) / totalFocusedWeight;
            DirectionScoring.AddAlignment(directions, entry.Direction, normalizedWeight, perTargetScores,
                raw: settings.HasVerticalMovement);
        }

        if (resolution > 1f - Epsilon) { return perTargetScores; }

        foreach (var dir in directions.Directions)
        {
            combineFirst[dir] = Mathf.Lerp(combineFirst.GetValueOrDefault(dir, 0f),
                perTargetScores.GetValueOrDefault(dir, 0f), resolution);
        }
        return combineFirst;
    }

    /// <summary>
    /// The raw weight of one member: confidence, and closing speed when the caller weights
    /// approach. The closing-speed dot uses the target's own velocity, not agent-relative speed.
    /// </summary>
    public static float MemberWeight(in TargetMember member, Vector3 toTargetDirection,
        in TargetResponseSettings settings)
    {
        float weight = 1f;

        if (settings.ConfidenceWeighted)
        {
            weight *= member.Confidence;
        }

        if (settings.ApproachSpeedWeight > Epsilon)
        {
            float closingSpeed = Mathf.Max(0f, member.Velocity.Dot(-toTargetDirection));
            weight *= 1f + closingSpeed * 0.2f * settings.ApproachSpeedWeight;
        }

        return weight;
    }

    /// <summary>Applies focus relative to the dominant member's raw weight.</summary>
    private static float FocusWeight(float weight, float maxWeight, float focus)
    {
        if (focus < Epsilon) { return weight; }
        bool isDominant = Mathf.IsEqualApprox(weight, maxWeight);
        return isDominant ? weight : weight * (1f - focus);
    }

    /// <summary>The member's position after the caller's projection influence is applied.</summary>
    private static Vector3 EffectivePosition(in TargetMember member, float projectionInfluence)
        => member.Position + (member.ProjectedPosition - member.Position) * projectionInfluence;
}
