namespace Jmodot.Implementation.Shared;

/// <summary>
/// One reject-second-claimant discipline, shared by every exclusive-authority slot in the
/// framework: the first owner keeps the slot; a conflicting owner is rejected (returns false
/// + warns); the owner (or a reset) releases it. Per-agent state lives here as a private field
/// on the owning Node, never on a shared Resource.
///
/// The subsystem tag prefixes every warning so the log line identifies the claiming system;
/// the slot name distinguishes multiple slots owned by that same system.
/// </summary>
public sealed class OwnedSlot<T>
{
    private readonly string _subsystemTag;

    public OwnedSlot(string subsystemTag)
    {
        _subsystemTag = subsystemTag;
    }

    public StringName? Owner { get; private set; }
    public T? Value { get; private set; }
    public bool IsClaimed => Owner != null;

    public bool TryClaim(StringName owner, T value, Node context, string slotName)
    {
        if (Owner != null && Owner != owner)
        {
            JmoLogger.Warning(context, $"[{_subsystemTag}] {slotName} claim held by '{Owner}'; '{owner}' rejected.");
            return false;
        }
        Owner = owner;
        Value = value;
        return true;
    }

    public bool TryRelease(StringName owner, Node context, string slotName)
    {
        if (Owner != owner)
        {
            JmoLogger.Warning(context, $"[{_subsystemTag}] '{owner}' tried to release {slotName} held by '{Owner?.ToString() ?? "no one"}'.");
            return false;
        }
        Clear();
        return true;
    }

    public void Clear()
    {
        Owner = null;
        Value = default;
    }
}
