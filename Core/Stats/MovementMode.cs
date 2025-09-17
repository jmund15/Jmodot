namespace Jmodot.Core.Stats;

/// <summary>
///     A simple, data-driven "tag" Resource that represents a state of movement
///     (e.g., "Ground", "Air", "Swimming"). It is used as a type-safe key in dictionaries,
///     replacing brittle enums or strings and allowing designers to define new movement types.
/// </summary>
[GlobalClass]
public partial class MovementMode : Resource
{
    [Export] public string ModeName { get; private set; } = "Unnamed";
}
