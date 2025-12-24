namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Environment;

/// <summary>
///     A component that should be attached to any character or actor that can be affected
///     by external environmental forces. It uses a Godot Area3D to detect and collect
///     all active IForceProviders in its vicinity, aggregating their effects into a
///     single, clean vector that the MovementProcessor can query.
/// </summary>
[GlobalClass]
public partial class ExternalForceReceiver3D : Area3D
{
    // Using a HashSet provides efficient add/remove operations and prevents duplicates.
    private readonly HashSet<IForceProvider3D> _activeAreaProviders = new();
    private readonly HashSet<IForceProvider3D> _internalProviders = new();

    public override void _Ready()
    {
        // Ensure this component does not collide with the character's own physics layers.
        // It should only interact with layers designated for environmental effects.

        // Connect to signals for automatic tracking of force providers.
        this.AreaEntered += this.OnProviderEntered;
        this.AreaExited += this.OnProviderExited;
    }

    public void RegisterInternalProvider(IForceProvider3D provider)
    {
        _internalProviders.Add(provider);
    }

    public void UnregisterInternalProvider(IForceProvider3D provider)
    {
        _internalProviders.Remove(provider);
    }

    private void OnProviderEntered(Area3D area)
    {
        if (area is IForceProvider3D provider)
        {
            this._activeAreaProviders.Add(provider);
        }
    }

    private void OnProviderExited(Area3D area)
    {
        if (area is IForceProvider3D provider)
        {
            this._activeAreaProviders.Remove(provider);
        }
    }

    /// <summary>
    ///     Calculates the total aggregated force from all currently active environmental zones.
    ///     This is the primary public API for this component.
    /// </summary>
    /// <param name="target">The actor being affected, passed to the force provider for context.</param>
    /// <returns>A single Vector3 representing the sum of all external forces for this frame.</returns>
    public Vector3 GetTotalForce(Node3D target)
    {
        var totalForce = Vector3.Zero;

        foreach (var provider in this._activeAreaProviders)
        {
            totalForce += provider.GetForceFor(target);
        }

        foreach (var provider in this._internalProviders)
        {
            totalForce += provider.GetForceFor(target);
        }

        return totalForce;
    }
}
