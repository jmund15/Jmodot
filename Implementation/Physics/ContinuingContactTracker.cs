namespace Jmodot.Implementation.Physics;

using System.Collections.Generic;

/// <summary>
/// Body-owned contact history. Call <see cref="Advance"/> once per movement step, then
/// <see cref="TryBeginContact"/> for each collider. A contact continues while seen in consecutive steps;
/// a duplicate within a step is also continuing. A step without it permits a new impact next step.
/// </summary>
public sealed class ContinuingContactTracker
{
    private HashSet<ulong> _previous = new();
    private HashSet<ulong> _current = new();

    public void Advance()
    {
        (_previous, _current) = (_current, _previous);
        _current.Clear();
    }

    public bool TryBeginContact(ulong colliderId)
        => _current.Add(colliderId) && !_previous.Contains(colliderId);
}
