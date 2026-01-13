namespace Jmodot.Implementation.Stats;

using Core.Stats;

/// <summary>
/// A component that detects and manages temporary environmental stat modifiers.
/// It uses an Area2D to find all active IStatContextProviders (e.g., ice patches,
/// mud pits) and instructs the character's IStatProvider to apply or remove their
/// associated stat modifiers. This component is the bridge between the environment
/// and the character's core stat system.
/// </summary>
[GlobalClass]
public partial class StatContextReceiver2D : Area2D
{
    // In the Godot editor, you must link this to the Node that has your StatController script.
    [Export]
    private Node _statProviderNode;
    private IStatProvider _statProvider;

    public override void _Ready()
    {
        // Ensure we have a valid reference to the IStatProvider.
        _statProvider = _statProviderNode as IStatProvider;
        if (_statProvider == null)
        {
            GD.PushError($"StatContextReceiver2D on '{Owner.Name}': The 'Stat Provider Node' is not set or does not implement IStatProvider.");
            SetProcess(false); // Disable the component if not set up correctly.
            return;
        }

        // Connect to signals for automatic detection.
        this.AreaEntered += this.OnProviderEntered;
        this.AreaExited += this.OnProviderExited;
    }

    /// <summary>
    /// Called by Godot when this Area2D overlaps with another.
    /// </summary>
    private void OnProviderEntered(Area2D area)
    {
        // Check if the area we entered is a stat context provider.
        if (area is not IStatContextProvider provider) return;

        // Apply all modifiers from the provider.
        // The provider's own instance (the Area2D node) is used as the unique "owner".
        // This is the key to the declarative cleanup system. The receiver doesn't need
        // to store handles because it will use RemoveAllModifiersFromSource on exit.
        foreach (var (attribute, modifierResource) in provider.Modifiers)
        {
            _statProvider.TryAddModifier(attribute, modifierResource, provider, out var handle);
        }
    }

    /// <summary>
    /// Called by Godot when this Area2D stops overlapping with another.
    /// </summary>
    private void OnProviderExited(Area2D area)
    {
        // Check if the area we are leaving is a stat context provider.
        if (area is not IStatContextProvider provider) return;

        // This single, clean call tells the StatController to find and remove
        // ALL modifiers that were previously added by this specific provider instance.
        // It's unambiguous, robust, and requires no local state tracking in this component.
        _statProvider.RemoveAllModifiersFromSource(provider);
    }
}
