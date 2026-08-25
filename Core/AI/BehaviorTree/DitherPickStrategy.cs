namespace Jmodot.Core.AI.BehaviorTree;

using Jmodot.Core.Movement;
using Jmodot.Core.Shared;

/// <summary>
/// Everything a dither pick is allowed to read. The stream arrives here because the strategy is a
/// shared authored <see cref="Resource"/>: the per-agent action resolves its seeded stream once and
/// hands it down, so no strategy resolves or latches anything of its own. That is what keeps one
/// authored <c>.tres</c> safe to serve every agent dithering at the same instant — RNG state held on
/// the Resource would make every one of them flip in lockstep.
/// </summary>
public readonly struct DitherPickContext
{
    /// <summary>The set the pick must choose a member of. Never null at the call site.</summary>
    public DirectionSet3D Directions { get; init; }

    /// <summary>The owning action's per-agent stream. The only randomness source a pick may use.</summary>
    public IRng Rng { get; init; }

    /// <summary>How many flips this dither has already performed; 0 on the first pick after enter.</summary>
    public int FlipIndex { get; init; }
}

/// <summary>
/// Which member of a direction set the next dither flip selects. Swapping this Resource is the whole
/// of retargeting a dither — the owning action keeps varying only <em>when</em> to flip and how long
/// to hold.
/// <para>
/// Implementations MUST be stateless: no cached stream, no per-agent counter. Per-flip and per-agent
/// state belongs to the action that owns the dither and arrives through
/// <see cref="DitherPickContext"/>.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class DitherPickStrategy : Resource
{
    /// <summary>
    /// The direction this flip should steer along. Returns <see cref="Vector3.Zero"/> when the set is
    /// empty; callers treat zero as "no steer this flip", never as an error.
    /// </summary>
    public abstract Vector3 Pick(in DitherPickContext ctx);
}
