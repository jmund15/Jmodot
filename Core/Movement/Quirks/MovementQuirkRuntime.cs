namespace Jmodot.Core.Movement.Quirks;

using Shared;

/// <summary>
/// Per-agent mutable state for one movement quirk, owned by the per-instance quirk processor.
/// Quirk Resources themselves hold zero per-agent state, so every value that varies between two
/// agents sharing a <c>.tres</c> lives on a subclass of this type.
/// <para>
/// The RNG lives on the base so the determinism contract is structural: every quirk draws from an
/// injected <see cref="IRng" /> rather than allocating its own.
/// </para>
/// </summary>
public abstract class MovementQuirkRuntime
{
    protected MovementQuirkRuntime(IRng rng) => Rng = rng;

    public IRng Rng { get; }
}
