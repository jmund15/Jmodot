namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Environment;
using Core.Pooling;

/// <summary>
///     A component that should be attached to any character or actor that can be affected
///     by external environmental forces and velocity offsets. It uses a Godot Area3D to detect
///     and collect all active providers in its vicinity, aggregating their effects into
///     clean vectors that the MovementProcessor can query.
/// </summary>
[GlobalClass]
public partial class ExternalForceReceiver3D : Area3D, IPoolResetable
{
    // Using HashSets provides efficient add/remove operations and prevents duplicates.
    private readonly HashSet<IForceProvider3D> _activeAreaProviders = new();
    private readonly HashSet<IForceProvider3D> _internalProviders = new();
    private readonly HashSet<IVelocityOffsetProvider3D> _activeOffsetProviders = new();

    // Track signal connection state - Godot signals throw errors when disconnecting nonexistent handlers
    private bool _signalsConnected;

    public override void _Ready()
    {
        // Connect signals only if not already connected (prevents duplicates on pool reuse)
        if (!_signalsConnected)
        {
            this.AreaEntered += OnProviderEntered;
            this.AreaExited += OnProviderExited;
            _signalsConnected = true;
        }
    }

    /// <summary>
    /// Resets state for object pooling. Called automatically via IPoolResetable when parent returns to pool.
    /// CRITICAL: Prevents stale external references after multiple pool cycles.
    /// </summary>
    public void OnPoolReset()
    {
        // 1. Clear EXTERNAL provider sets only - prevents stale references to areas the spell was in
        // NOTE: DO NOT clear _internalProviders! These are child nodes (e.g., GravityProviderCapability)
        // that persist across pool cycles and register themselves once in _Ready().
        // Clearing them would break gravity since _Ready() doesn't re-run on pool activation.
        _activeAreaProviders.Clear();
        _activeOffsetProviders.Clear();

        // 2. Disconnect signals to prevent duplication on next activation
        // NOTE: Signals stay connected across pool cycles since _signalsConnected persists.
        // This is intentional - the handlers are still valid and we just clear the provider sets.
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
    /// <returns>A single Vector3 representing the sum of all velocity offsets for this frame.</returns>
    public Vector3 GetTotalVelocityOffset(Node3D target)
    {
        var totalOffset = Vector3.Zero;

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
