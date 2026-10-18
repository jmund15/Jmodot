namespace Jmodot.Implementation.AI.Navigation.Targets;

using System;
using Core.AI.BB;
using Core.AI.Perception;
using Godot;
using Implementation.AI.BB;
using Implementation.AI.Perception;
using Core.Shared;

/// <summary>
/// A reusable description of what a goal or consideration steers at or away from. A target is
/// shared across agents; every value that varies per agent lives on the caller's
/// <see cref="TargetRuntime"/>, never on this Resource.
/// </summary>
[GlobalClass, Tool]
public abstract partial class MovementTarget3D : Resource, IResourceConfigurationWarnings, IAgentConfigurationWarnings
{
    /// <summary>
    /// Fills <paramref name="sample"/> with the eligible members this target currently sees.
    /// The sample is cleared first, so a failed resolve leaves no stale member behind.
    /// </summary>
    public abstract void Resolve(Vector3 agentPosition, IBlackboard bb,
        AIPerceptionManager3D? memory, TargetSample sample);

    /// <summary>The authored shape of this target; a live sample may still report otherwise.</summary>
    public virtual TargetSampleGeometry Geometry => TargetSampleGeometry.Member;

    /// <summary>
    /// Faults that need the entity this target is mounted on, such as a missing perception
    /// manager. The owning goal forwards this, so no host tests a target type.
    /// </summary>
    public virtual string[] GetAgentWarnings(Node agent) => Array.Empty<string>();

    public virtual string[] GetResourceConfigurationWarnings() => Array.Empty<string>();

    /// <summary>Resolves <paramref name="target"/>, or the primary target when it is unset.</summary>
    public static void ResolveOrPrimary(MovementTarget3D? target, Vector3 agentPosition,
        IBlackboard bb, AIPerceptionManager3D? memory, TargetSample sample)
    {
        if (target != null)
        {
            target.Resolve(agentPosition, bb, memory, sample);
            return;
        }

        sample.Clear();
        if (TryReadTracked(BBDataSig.PrimaryTargetMemory, agentPosition, bb, out var member))
        {
            sample.SetSingle(member);
            return;
        }

        sample.State = TargetSampleState.Empty;
    }

    /// <summary>
    /// The one tracked read: a live memory under <paramref name="key"/>, captured as a member.
    /// Missing, inactive, wrong-type and freed memories all fail the same read.
    /// </summary>
    internal static bool TryReadTracked(StringName key, Vector3 agentPosition, IBlackboard bb,
        out TargetMember member)
    {
        if (bb.TryGetLiveTarget(key, out var memory))
        {
            member = TargetMember.FromMemory(memory, agentPosition);
            return true;
        }

        member = default;
        return false;
    }
}
