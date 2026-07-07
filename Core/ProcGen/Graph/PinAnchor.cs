namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     Base for a polymorphic pin-anchor family: resolves the spine index a
///     <see cref="PinnedPlacement" /> targets. A load-bearing base (not a members-less marker) —
///     the single abstract projection <see cref="ResolveSpineIndex" /> is consumed polymorphically
///     by config validation and the generator, never downcast in the engine (BOUNDARY invariant).
///     <para>
///         The P3b geometry layer adds positional/area anchors as further subclasses, which resolve
///         their index relative to the passed config context — hence the
///         <see cref="ISkeletonConfig" /> parameter even though <see cref="SpineIndexAnchor" />
///         ignores it.
///     </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class PinAnchor : Resource
{
    /// <summary>
    ///     Pure projection to the spine index this anchor targets, resolved against the full config
    ///     context plus the spine length actually in play. Side-effect-free; the index is
    ///     range-checked by the consuming <see cref="ISkeletonConfig.Validate" /> (which passes the
    ///     authored <c>Length.Max</c> as the length bound) and re-resolved by the generator against
    ///     the drawn length, not here.
    /// </summary>
    /// <param name="drawnSpineLength">
    ///     The spine length the caller is resolving against: the seed-drawn length inside the
    ///     generator, or the authored maximum during config validation. Positional anchors
    ///     (<see cref="SpineIndexAnchor" />) ignore it; relative anchors (fraction/geometry) scale by it.
    /// </param>
    public abstract int ResolveSpineIndex(ISkeletonConfig config, int drawnSpineLength);
}
