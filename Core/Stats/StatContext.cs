namespace Jmodot.Core.Stats;

/// <summary>
/// A data-driven "tag" Resource that represents a specific character state or
/// environmental context (e.g., "InAir", "Swimming", "OnFire"). It is used as a
/// type-safe key in dictionaries (like in the CharacterArchetype) and as an "owner"
/// token when applying and removing contextual stat modifiers via the IStatProvider.
/// </summary>
[GlobalClass]
public partial class StatContext : Resource
{
    /// <summary>
    /// A user-friendly name for debugging and editor identification purposes.
    /// </summary>
    [Export]
    public string ContextName { get; private set; } = "Unnamed Context";
}
