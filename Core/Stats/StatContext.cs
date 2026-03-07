namespace Jmodot.Core.Stats;

using Godot.Collections;

/// <summary>
/// A self-contained Resource that represents a specific character state or
/// environmental context (e.g., "InAir", "Swimming", "OnFire"). It carries
/// its own stat modifiers and acts as the "owner" token when applying and
/// removing those modifiers via the IStatProvider.
/// </summary>
[GlobalClass, Tool]
public partial class StatContext : Resource
{
    /// <summary>
    /// A user-friendly name for debugging and editor identification purposes.
    /// </summary>
    [Export]
    public string ContextName { get; private set; } = "Unnamed Context";

    /// <summary>
    /// The stat modifiers applied while this context is active.
    /// Key: the Attribute to target. Value: a Resource implementing IModifier.
    /// </summary>
    [Export]
    public Dictionary<Attribute, Resource> Modifiers { get; private set; } = new();
}
