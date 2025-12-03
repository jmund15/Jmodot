using Godot;

namespace Jmodot.Core.Combat;

/// <summary>
/// A unique identifier for gameplay concepts (e.g., "Stun", "Poison", "Fire").
/// Used to tag Status Effects and drive State Machine transitions.
/// </summary>
[GlobalClass]
public partial class GameplayTag : Resource
{
    /// <summary>
    /// String Identifier of the GameTag. Used for matching in combat effects
    /// </summary>
    [Export] public StringName TagId { get; set; } = null!;

    // We can add hierarchy here later if needed (e.g., Parent Tag)

    /// <summary>
    /// Priority of this tag for reaction resolution (e.g., Stun > Damage).
    /// Lower value = Higher priority.
    /// </summary>
    [Export] public int Priority { get; set; }
}
