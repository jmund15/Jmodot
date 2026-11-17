namespace Jmodot.Implementation.Stats;

using Core.Modifiers;
using Core.Shared.Attributes;
using Core.Stats;
using Shared;
using Shared.GodotExceptions;

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
    /// <summary>The node implementing <see cref="IStatProvider"/> whose stats the entered providers modify.</summary>
    [Export, RequiredExport]
    private Node _statProviderNode = null!;
    private IStatProvider _statProvider = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        if (_statProviderNode is not IStatProvider statProvider)
        {
            throw JmoLogger.LogAndRethrow(new NodeConfigurationException(
                    $"'Stat Provider Node' ({_statProviderNode.Name}) does not implement {nameof(IStatProvider)}.", this),
                this);
        }
        _statProvider = statProvider;

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
        if (area is not IStatContextProvider provider) { return; }

        this.ApplyProviderModifiers(provider);
    }

    /// <summary>
    /// Applies every modifier a provider carries, owned by the provider instance so
    /// RemoveAllModifiersFromSource can retract the whole set on exit.
    /// </summary>
    private void ApplyProviderModifiers(IStatContextProvider provider)
    {
        // Apply all modifiers from the provider.
        // The provider's own instance (the Area2D node) is used as the unique "owner".
        // This is the key to the declarative cleanup system. The receiver doesn't need
        // to store handles because it will use RemoveAllModifiersFromSource on exit.
        foreach (var (attribute, modifier) in provider.Modifiers)
        {
            var typed = AttributeModifier.FromUntyped(modifier, attribute, this);
            if (typed == null) { continue; }
            _statProvider.TryAddModifier(attribute, typed, provider, out var handle);
        }
    }

    /// <summary>
    /// Called by Godot when this Area2D stops overlapping with another.
    /// </summary>
    private void OnProviderExited(Area2D area)
    {
        // Check if the area we are leaving is a stat context provider.
        if (area is not IStatContextProvider provider) { return; }

        // This single, clean call tells the StatController to find and remove
        // ALL modifiers that were previously added by this specific provider instance.
        // It's unambiguous, robust, and requires no local state tracking in this component.
        _statProvider.RemoveAllModifiersFromSource(provider);
    }

    #region Test Helpers
#if TOOLS
    internal void SetStatProviderForTest(IStatProvider provider) => this._statProvider = provider;
    internal void _TestApplyProviderModifiers(IStatContextProvider provider) => this.ApplyProviderModifiers(provider);
#endif
    #endregion
}
