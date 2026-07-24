namespace Jmodot.Core.Environment.FlowField;

using System.Collections.Generic;

/// <summary>
///     Runtime state for one flow-field stream: a chain of segments plus the bookkeeping a manager needs
///     to decay, merge, bound and pool it.
/// </summary>
public sealed class FlowFieldEntity
{
    public int Id;

    /// <summary>
    ///     Bumped at EVERY retirement path (merge-absorb, overflow eviction, dormancy expiry, clear-all) at
    ///     retirement time rather than at pool reuse, so an outstanding handle referencing a recycled Id
    ///     fails its generation check instead of silently writing into a stranger's stream.
    /// </summary>
    public int Generation;

    public readonly List<FlowSegment> Segments = new();

    /// <summary>
    ///     Configuration for this stream, held through the framework's own abstraction so no consumer type
    ///     is referenced. Deliberately stored ON the entity rather than in a manager-side id-to-profile side
    ///     map: that map would need invalidating on all four retirement paths, and a recycled Id would
    ///     inherit a stale profile — corrupting the very reference-identity merge gate it feeds.
    /// </summary>
    public IFlowFieldProfile? Profile;

    /// <summary>Cached sum of segment energy, refreshed by the owning manager's bookkeeping tick.</summary>
    public float TotalEnergy;

    /// <summary>Grows synchronously when a deposit lands outside it; shrink is deferred to the tick.</summary>
    public Aabb Bounds;

    /// <summary>Hysteresis-banded: entered below the enter threshold, left above the exit threshold.</summary>
    public bool Dormant;

    /// <summary>Consumed by the decay grace window — decay pauses while the stream is actively fed.</summary>
    public uint LastDepositTick;
}
