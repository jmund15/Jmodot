namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Environment;
using Core.Pooling;

/// <summary>
///     A component that should be attached to any character or actor that can be affected
///     by external environmental forces and velocity offsets. It uses a Godot Area2D to detect
///     and collect all active providers in its vicinity, aggregating their effects into
///     clean vectors that the MovementProcessor can query.
/// </summary>
[GlobalClass]
public partial class ExternalForceReceiver2D : Area2D, IPoolResetable
{
    private readonly HashSet<IForceProvider2D> _activeAreaProviders = new();
    private readonly HashSet<IForceProvider2D> _internalProviders = new();
    private readonly HashSet<IVelocityOffsetProvider2D> _activeOffsetProviders = new();

    private bool _signalsConnected;

    public override void _Ready()
    {
        if (!_signalsConnected)
        {
            this.AreaEntered += OnProviderEntered;
            this.AreaExited += OnProviderExited;
            _signalsConnected = true;
        }
    }

    /// <summary>
    /// Resets state for object pooling. Called automatically via IPoolResetable when parent returns to pool.
    /// </summary>
    public void OnPoolReset()
    {
        _activeAreaProviders.Clear();
        _activeOffsetProviders.Clear();
    }

    public void RegisterInternalProvider(IForceProvider2D provider)
    {
        _internalProviders.Add(provider);
    }

    public void UnregisterInternalProvider(IForceProvider2D provider)
    {
        _internalProviders.Remove(provider);
    }

    private void OnProviderEntered(Area2D area)
    {
        if (area is IForceProvider2D forceProvider)
        {
            _activeAreaProviders.Add(forceProvider);
        }

        if (area is IVelocityOffsetProvider2D offsetProvider)
        {
            _activeOffsetProviders.Add(offsetProvider);
        }
    }

    private void OnProviderExited(Area2D area)
    {
        if (area is IForceProvider2D forceProvider)
        {
            _activeAreaProviders.Remove(forceProvider);
        }

        if (area is IVelocityOffsetProvider2D offsetProvider)
        {
            _activeOffsetProviders.Remove(offsetProvider);
        }
    }

    /// <summary>
    /// Finds the force provider contributing the strongest force to the target.
    /// Returns the provider as a Node2D for BB storage, or null if no providers are active.
    /// </summary>
    public (Node2D? source, Vector2 force) GetDominantForceSource(Node2D target)
    {
        Node2D? dominantSource = null;
        var dominantForce = Vector2.Zero;
        var maxMagnitudeSq = 0f;

        foreach (var provider in _activeAreaProviders)
        {
            if (!GodotObject.IsInstanceValid((GodotObject)provider))
            {
                continue;
            }

            var force = provider.GetForceFor(target);
            var magSq = force.LengthSquared();
            if (magSq > maxMagnitudeSq)
            {
                maxMagnitudeSq = magSq;
                dominantForce = force;
                dominantSource = provider as Node2D;
            }
        }

        foreach (var provider in _internalProviders)
        {
            if (!GodotObject.IsInstanceValid((GodotObject)provider))
            {
                continue;
            }

            var force = provider.GetForceFor(target);
            var magSq = force.LengthSquared();
            if (magSq > maxMagnitudeSq)
            {
                maxMagnitudeSq = magSq;
                dominantForce = force;
                dominantSource = provider as Node2D;
            }
        }

        // Also check velocity offset providers (drag/currents are offset-based)
        foreach (var provider in _activeOffsetProviders)
        {
            if (!GodotObject.IsInstanceValid((GodotObject)provider))
            {
                continue;
            }

            var offset = provider.GetVelocityOffsetFor(target);
            var magSq = offset.LengthSquared();
            if (magSq > maxMagnitudeSq)
            {
                maxMagnitudeSq = magSq;
                dominantForce = offset;
                dominantSource = provider as Node2D;
            }
        }

        return (dominantSource, dominantForce);
    }

    /// <summary>
    ///     Calculates the total aggregated force from all currently active environmental zones.
    /// </summary>
    /// <param name="target">The actor being affected, passed to the force provider for context.</param>
    /// <returns>A single Vector2 representing the sum of all external forces for this frame.</returns>
    public Vector2 GetTotalForce(Node2D target)
    {
        var totalForce = Vector2.Zero;

        foreach (var provider in _activeAreaProviders)
        {
            if (GodotObject.IsInstanceValid((GodotObject)provider))
            {
                totalForce += provider.GetForceFor(target);
            }
        }

        foreach (var provider in _internalProviders)
        {
            if (GodotObject.IsInstanceValid((GodotObject)provider))
            {
                totalForce += provider.GetForceFor(target);
            }
        }

        return totalForce;
    }

    /// <summary>
    ///     Gets total velocity offset from all active offset providers.
    ///     Offsets are NOT stored - calculated fresh each frame for friction-independent effects.
    /// </summary>
    /// <param name="target">The actor being affected.</param>
    /// <returns>A single Vector2 representing the sum of all velocity offsets for this frame.</returns>
    public Vector2 GetTotalVelocityOffset(Node2D target)
    {
        var totalOffset = Vector2.Zero;

        foreach (var provider in _activeOffsetProviders)
        {
            if (GodotObject.IsInstanceValid((GodotObject)provider))
            {
                totalOffset += provider.GetVelocityOffsetFor(target);
            }
        }

        return totalOffset;
    }
}
