namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using Shared;

/// <summary>
/// Abstract base class for all sprite animation variant sources.
/// These are data-driven Resources that provide a string component (e.g., "_north", "_sword")
/// to be used by an AnimationNamingConvention to construct a final animation name.
/// </summary>
// [Tool] on the base as well as the concrete: a C# Resource only runs in the editor when every
// class in its chain carries the attribute, and these are authored as .tres in the Inspector.
[GlobalClass, Tool]
public abstract partial class AnimVariantSource : Resource
{
    /// <summary>
    /// The order in which this variant should be applied by the naming convention.
    /// Lower numbers are applied first.
    /// </summary>
    [Export]
    public int Order { get; set; } = 0;

    /// <summary>
    /// Gets the current string variant provided by this source.
    /// This value is calculated and cached internally when state is updated.
    /// </summary>
    /// <returns>The animation variant string (e.g., "_N"), or an empty string if not applicable.</returns>
    public abstract string GetAnimVariant();

    /// <summary>
    /// Recomputes this source's contribution for a base name the orchestrator is about to compose,
    /// and returns it — empty when this source does not vary <paramref name="baseName"/>.
    /// Called at the composition site itself so a per-play ordinal or roll is never latched earlier
    /// than the decision it feeds.
    /// </summary>
    /// <remarks>
    /// The default forwards to <see cref="GetAnimVariant"/>, so a source whose state is pushed in by
    /// some other collaborator (direction, equipment style) behaves exactly as before.
    /// </remarks>
    public virtual string SelectAnimVariant(StringName baseName) => GetAnimVariant();

    /// <summary>
    /// The seed-stream kind this source needs a per-entity <see cref="IRng"/> for, or null when it
    /// draws no randomness. Named by the source rather than by the orchestrator so a game-side
    /// concrete can point at its own key registry without the framework referencing it.
    /// </summary>
    public virtual string? RngSeedKind => null;

    /// <summary>
    /// Supplies the per-entity stream named by <see cref="RngSeedKind"/>, once, at entity
    /// initialization. Default no-op: a source that names no kind is never handed one.
    /// </summary>
    public virtual void SetRng(IRng rng) { }
}
