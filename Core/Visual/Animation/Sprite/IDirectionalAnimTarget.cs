namespace Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// An animation target that resolves a <see cref="DirectionalAnimRequest"/> per-slave, letting
/// each slave degrade independently under its own <see cref="SlotFallbackPolicy"/>. Implemented by
/// the composite so the orchestrator can type-test and delegate directional fan-out instead of
/// pushing a single pre-resolved name through naive partial-match.
/// </summary>
public interface IDirectionalAnimTarget
{
    /// <summary>Hard-resets each slave to its resolved clip. Returns whether the master resolved.</summary>
    bool StartAnimDirectional(DirectionalAnimRequest request);

    /// <summary>Updates each slave to its resolved clip preserving time per <paramref name="mode"/>. Returns whether the master resolved.</summary>
    bool UpdateAnimDirectional(DirectionalAnimRequest request, AnimUpdateMode mode);
}
