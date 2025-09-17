namespace Jmodot.Core.AI.Affinities;

/// <summary>
///     A data-driven "tag" Resource that represents a core personality trait or emotional
///     state (e.g., "Fear", "Anger", "Recklessness"). It is used as a type-safe key in
///     dictionaries, replacing brittle enums or strings and allowing designers to define new
///     personality traits without changing code.
/// </summary>
[GlobalClass]
public partial class Affinity : Resource
{
    [Export] public string AffinityName { get; private set; } = "Unnamed Affinity";
}
