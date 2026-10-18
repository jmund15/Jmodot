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
    /// Key: the Attribute to target. Value: a Resource implementing IModifier. Every read first runs
    /// <see cref="PrepareModifiers"/>, so a preset that generates its payload can build it on first use.
    /// </summary>
    [Export]
    public Dictionary<Attribute, Resource> Modifiers
    {
        get
        {
            PrepareModifiers();
            return _modifiers;
        }
        private set => _modifiers = value;
    }

    private Dictionary<Attribute, Resource> _modifiers = new();

    /// <summary>Hook run before each read of <see cref="Modifiers"/>; a no-op unless a preset generates its payload.</summary>
    protected virtual void PrepareModifiers() { }
}
