namespace Jmodot.Implementation.AI.Navigation.Targets;

using System;
using Core.AI.BB;
using Core.AI.Perception;
using Godot;
using Implementation.AI.BB;
using Implementation.AI.Perception;
using Implementation.Shared;

/// <summary>
/// Follows one perception memory published on the blackboard. With no override the key is the
/// primary target, so a goal or consideration needs no authored key to follow what the entity
/// already targets.
/// </summary>
[GlobalClass, Tool]
public partial class TrackedTarget3D : MovementTarget3D
{
    /// <summary>
    /// Blackboard key holding the <see cref="Perception3DInfo"/> of one target. Defaults to the
    /// primary-target memory; an explicitly empty key is a configuration fault.
    /// </summary>
    [Export] public StringName Key { get; private set; } = Jmodot.Implementation.AI.BB.BBDataSig.PrimaryTargetMemory;

    public override void Resolve(Vector3 agentPosition, IBlackboard bb,
        AIPerceptionManager3D? memory, TargetSample sample)
    {
        sample.Clear();

        if (Key == null || Key.IsEmpty)
        {
            sample.State = TargetSampleState.Unavailable;
            if (sample.TryBeginLog(TargetLogKind.EmptyKey))
            {
                JmoLogger.Error(this,
                    $"TrackedTarget3D '{ResourceName}' has an empty Key. Assign the blackboard key to " +
                    "follow, or remove this target override to follow the primary target.");
            }
            return;
        }

        if (TryReadTracked(Key, agentPosition, bb, out var member))
        {
            sample.SetSingle(member);
            return;
        }

        sample.State = TargetSampleState.Empty;
        if (Key == Jmodot.Implementation.AI.BB.BBDataSig.PrimaryTargetMemory) { return; }
        if (bb.TryGet<Perception3DInfo>(Key, out var published) && published != null) { return; }

        if (sample.TryBeginLog(TargetLogKind.MissingKey))
        {
            JmoLogger.Warning(this,
                $"TrackedTarget3D '{ResourceName}' reads blackboard key '{Key}' but nothing is published " +
                "under it. Check the key against the one the target tracker writes; this target stays empty " +
                "until a memory appears.");
        }
    }

    public override string[] GetResourceConfigurationWarnings()
        => Key == null || Key.IsEmpty
            ? new[]
            {
                "Key is empty. Assign the blackboard key to follow, or remove this target override to " +
                "follow the primary target.",
            }
            : Array.Empty<string>();

    #region Test Helpers
#if TOOLS
    internal void SetKeyForTest(StringName key) => Key = key;
#endif
    #endregion
}
