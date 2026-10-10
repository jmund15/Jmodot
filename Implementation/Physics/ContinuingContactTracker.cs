namespace Jmodot.Implementation.Physics;

using System.Collections.Generic;

/// <summary>
/// Body-owned contact history. Call <see cref="Advance"/> once per movement step, then
/// <see cref="TryBeginContact"/> for each collider. A contact continues while seen in consecutive steps;
/// a duplicate within a step is also continuing. A step without it permits a new impact next step.
/// A contact whose impact resolved is also remembered as pushing for as long as it continues, so the body that
/// owns the tracker keeps carrying its target while it presses on.
/// </summary>
public sealed class ContinuingContactTracker
{
    private HashSet<ulong> _previous = new();
    private HashSet<ulong> _current = new();
    private HashSet<ulong> _previousPushing = new();
    private HashSet<ulong> _currentPushing = new();

    public void Advance()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
        (_previousPushing, _currentPushing) = (_currentPushing, _previousPushing);
        _currentPushing.Clear();
    }

    /// <summary>True when this contact resolved an impact on the previous step and is still touching.</summary>
    public bool IsPushing(ulong colliderId) => _previousPushing.Contains(colliderId);

    /// <summary>Keeps a contact that resolved an impact eligible to push on the next step while it continues.</summary>
    public void MarkPushing(ulong colliderId) => _currentPushing.Add(colliderId);

    public bool TryBeginContact(ulong colliderId)
        => _current.Add(colliderId) && !_previous.Contains(colliderId);

    /// <summary>A contact that resolved nothing but can still close must remain eligible on the next movement step.</summary>
    public void CancelContact(ulong colliderId)
    {
        _current.Remove(colliderId);
        _currentPushing.Remove(colliderId);
    }
}
