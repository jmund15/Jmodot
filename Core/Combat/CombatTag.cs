using Jmodot.Core.Identification;
using Jmodot.Core.Shared.Attributes;

namespace Jmodot.Core.Combat;

/// <summary>
/// A unique identifier for gameplay concepts (e.g., "Stun", "Poison", "Fire").
/// Used to tag Status Effects and drive State Machine transitions.
/// Inherits from <see cref="Category"/> to participate in the category hierarchy,
/// enabling hierarchical matching (e.g., a "Burn" tag descends from "Fire" → "Elemental").
/// </summary>
[GlobalClass]
public partial class CombatTag : Category
{
    /// <summary>
    /// String Identifier of the GameTag. Used for matching in combat effects
    /// </summary>
    [Export, RequiredExport] public StringName TagId { get; set; } = null!;

    /// <summary>
    /// Priority of this tag for reaction resolution (e.g., Stun > Damage).
    /// Lower value = Higher priority.
    /// </summary>
    [Export] public int Priority { get; set; }

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
