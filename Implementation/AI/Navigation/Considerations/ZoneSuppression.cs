namespace Jmodot.Implementation.AI.Navigation.Considerations;

using Core.AI.BB;
using Godot;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.Shared;

/// <summary>
/// The one owner of "suppress while a zone is active": a blackboard Vector4 (centre xyz, radius in
/// W) whose positive radius silences a goal or consideration's pull. Every steering surface that
/// exposes a zone key reads it here, so one key means one thing everywhere.
/// </summary>
internal static class ZoneSuppression
{
    /// <summary>
    /// True when <paramref name="key"/> names a published zone with a positive radius. An empty key
    /// or an unpublished one never suppresses. A published value that is not a Vector4 is a wiring
    /// fault: it suppresses too, and warns once on <paramref name="runtime"/> (every call without one).
    /// </summary>
    internal static bool IsSuppressed(IBlackboard bb, StringName? key, Resource owner, TargetRuntime? runtime)
    {
        if (key == null || key.IsEmpty) { return false; }
        if (bb.TryGet<Vector4>(key, out var zone)) { return zone.W > 0f; }
        if (!bb.ContainsLocal(key)) { return false; }

        if (runtime?.Sample.TryBeginLog(TargetLogKind.ZoneKeyWrongType) ?? true)
        {
            JmoLogger.Warning(owner,
                $"{owner.GetType().Name} '{owner.ResourceName}' binds zone key '{key}', but the blackboard " +
                "holds a value that is not a Vector4 (centre xyz + radius in W). It scores zero until " +
                "the publisher writes a Vector4.");
        }
        return true;
    }
}
