namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Environment;

/// <summary>
///     A component that should be attached to any character or actor that can be affected
///     by external environmental forces. It uses a Godot Area2D to detect and collect
///     all active IForceProviders in its vicinity, aggregating their effects into a
///     single, clean vector that the MovementProcessor can query.
/// </summary>
[GlobalClass]
public partial class ExternalForceReceiver2D : Area2D
{
    // Using a HashSet provides efficient add/remove operations and prevents duplicates.
    private readonly HashSet<IForceProvider2D> _activeForceProviders = new();

    public override void _Ready()
    {
        // Ensure this component does not collide with the character's own physics layers.
        // It should only interact with layers designated for environmental effects.

        // Connect to signals for automatic tracking of force providers.
        this.AreaEntered += this.OnProviderEntered;
        this.AreaExited += this.OnProviderExited;
    }

    private void OnProviderEntered(Area2D area)
    {
        if (area is IForceProvider2D provider)
        {
            this._activeForceProviders.Add(provider);
        }
    }

    private void OnProviderExited(Area2D area)
    {
        if (area is IForceProvider2D provider)
        {
            this._activeForceProviders.Remove(provider);
        }
    }

    /// <summary>
    ///     Calculates the total aggregated force from all currently active environmental zones.
    ///     This is the primary public API for this component.
    /// </summary>
    /// <param name="target">The actor being affected, passed to the force provider for context.</param>
    /// <returns>A single Vector2 representing the sum of all external forces for this frame.</returns>
    public Vector2 GetTotalForce(Node2D target)
    {
        if (this._activeForceProviders.Count == 0)
        {
            return Vector2.Zero;
        }

        var totalForce = Vector2.Zero;
        foreach (var provider in this._activeForceProviders)
        {
            totalForce += provider.GetForceFor(target);
        }

        return totalForce;
    }
}
