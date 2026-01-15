using Godot;
using Jmodot.Core.Identification;

namespace Jmodot.Core.Combat;

/// <summary>
/// A unique identifier for gameplay concepts (e.g., "Stun", "Poison", "Fire").
/// Used to tag Status Effects and drive State Machine transitions.
/// </summary>
[GlobalClass]
public partial class CombatTag : Resource
{
    /// <summary>
    /// String Identifier of the GameTag. Used for matching in combat effects
    /// </summary>
    [Export] public StringName TagId { get; set; } = null!;

    /// <summary>
    /// Priority of this tag for reaction resolution (e.g., Stun > Damage).
    /// Lower value = Higher priority.
    /// </summary>
    [Export] public int Priority { get; set; }

    /// <summary>
    /// Optional elemental/type category for label-based interactions.
    /// Links this tag to a Category (e.g., Fire, Water, Ice) so that
    /// interaction rules can apply to all effects sharing the same element.
    /// </summary>
    /// <remarks>
    /// Example: Both "Burn" and "Smolder" CombatTags can link to the same
    /// "Fire" category, so a "Water cancels Fire" interaction affects both.
    /// </remarks>
    [Export] public Category? ElementalCategory { get; set; }

    /// <summary>
    /// Optional stacking policy for effects with this tag.
    /// Defines how multiple instances of the same effect stack
    /// (max instances, overflow behavior, duration handling).
    /// </summary>
    /// <remarks>
    /// If null, effects use default behavior (unlimited stacking, independent durations).
    /// </remarks>
    [Export] public StackPolicy? StackPolicy { get; set; }
}
