namespace Jmodot.Core.Stats.Mechanics;

/// <summary>
///     A simple, data-driven "tag" Resource that represents a type of mechanic
///     (e.g., "Jump", "Wall Jump", "Ground Pound"). It is used as a type-safe key in dictionaries,
///     replacing brittle enums or strings and allowing designers to define new mechanic types.
/// </summary>
[GlobalClass]
public partial class MechanicType : Resource
{
    [Export] public string MechanicName { get; private set; } = "Unnamed Mechanic";
}