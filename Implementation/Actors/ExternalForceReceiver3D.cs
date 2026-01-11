namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Environment;

/// <summary>
///     A component that should be attached to any character or actor that can be affected
///     by external environmental forces and velocity offsets. It uses a Godot Area3D to detect
///     and collect all active providers in its vicinity, aggregating their effects into
///     clean vectors that the MovementProcessor can query.
/// </summary>
[GlobalClass]
public partial class ExternalForceReceiver3D : Area3D
{
    // Using HashSets provides efficient add/remove operations and prevents duplicates.
    private readonly HashSet<IForceProvider3D> _activeAreaProviders = new();
    private readonly HashSet<IForceProvider3D> _internalProviders = new();
    private readonly HashSet<IVelocityOffsetProvider3D> _activeOffsetProviders = new();

    public override void _Ready()
    {
        // Connect to signals for automatic tracking of providers.
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
        if (area is IForceProvider3D forceProvider)
        {
            _activeAreaProviders.Add(forceProvider);
        }

        if (area is IVelocityOffsetProvider3D offsetProvider)
        {
            _activeOffsetProviders.Add(offsetProvider);
        }
    }

    private void OnProviderExited(Area3D area)
    {
        if (area is IForceProvider3D forceProvider)
        {
            _activeAreaProviders.Remove(forceProvider);
        }

        if (area is IVelocityOffsetProvider3D offsetProvider)
        {
            _activeOffsetProviders.Remove(offsetProvider);
        }
    }

    /// <summary>
    ///     Calculates the total aggregated force from all currently active environmental zones.
    ///     Forces are stored in velocity and affected by friction next frame.
    /// </summary>
    /// <param name="target">The actor being affected, passed to the force provider for context.</param>
    /// <returns>A single Vector3 representing the sum of all external forces for this frame.</returns>
    public Vector3 GetTotalForce(Node3D target)
    {
        var totalForce = Vector3.Zero;

        foreach (var provider in _activeAreaProviders)
        {
            totalForce += provider.GetForceFor(target);
        }

        foreach (var provider in _internalProviders)
        {
            totalForce += provider.GetForceFor(target);
        }

        return totalForce;
    }

    /// <summary>
    ///     Gets total velocity offset from all active offset providers.
    ///     Offsets are NOT stored - calculated fresh each frame for friction-independent effects.
    /// </summary>
    /// <param name="target">The actor being affected.</param>
    /// <returns>A single Vector3 representing the sum of all velocity offsets for this frame.</returns>
    public Vector3 GetTotalVelocityOffset(Node3D target)
    {
        var totalOffset = Vector3.Zero;

        foreach (var provider in _activeOffsetProviders)
        {
            totalOffset += provider.GetVelocityOffsetFor(target);
        }

        return totalOffset;
    }
}
