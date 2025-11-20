namespace Jmodot.Core.Combat;

/// <summary>
/// A Resource that defines a type of combat effect (e.g., "PhysicalDamage", "Stun", "FireDOT").
/// Using a Resource allows designers to create new effect types without changing code.
/// </summary>
[GlobalClass]
public partial class CombatEffectType : Resource { }
