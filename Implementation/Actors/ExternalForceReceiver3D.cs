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
    /// Finds the force provider contributing the strongest force to the target.
    /// Returns the provider as a Node3D for BB storage, or null if no providers are active.
    /// </summary>
    public (Node3D? source, Vector3 force) GetDominantForceSource(Node3D target)
    {
        Node3D? dominantSource = null;
        var dominantForce = Vector3.Zero;
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
                dominantSource = provider as Node3D;
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
                dominantSource = provider as Node3D;
            }
        }

        // Also check velocity offset providers (wave drag is offset-based)
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
                dominantSource = provider as Node3D;
            }
        }

        return (dominantSource, dominantForce);
    }

    /// <summary>
    /// Yields the union of area + internal force providers that opt-in to capture
    /// detection via <see cref="IForceProvider3D.IsCaptureForce"/>. Single source of
    /// truth for the capture-vs-ambient filter; consumed by GetCaptureForce and
    /// DescribeActiveCaptureProviders so the predicate cannot drift between sites.
    /// </summary>
    private IEnumerable<IForceProvider3D> EnumerateActiveCaptureForceProviders()
    {
        foreach (var p in _activeAreaProviders)
        {
            if (p.IsCaptureForce && GodotObject.IsInstanceValid((GodotObject)p))
            {
                yield return p;
            }
        }

        foreach (var p in _internalProviders)
        {
            if (p.IsCaptureForce && GodotObject.IsInstanceValid((GodotObject)p))
            {
                yield return p;
            }
        }
    }

    /// <summary>
    /// Yields offset providers that opt-in to capture detection via
    /// <see cref="IVelocityOffsetProvider3D.IsCaptureOffset"/>.
    /// </summary>
    private IEnumerable<IVelocityOffsetProvider3D> EnumerateActiveCaptureOffsetProviders()
    {
        foreach (var p in _activeOffsetProviders)
        {
            if (p.IsCaptureOffset && GodotObject.IsInstanceValid((GodotObject)p))
            {
                yield return p;
            }
        }
    }

    /// <summary>
    /// Builds a one-line summary of currently-active capture providers and their
    /// per-provider force/offset magnitudes for diagnostic logging. Empty string when
    /// no capture providers are active. Cheap enough for log-on-transition use; do
    /// not call every physics tick.
    /// </summary>
    public string DescribeActiveCaptureProviders(Node3D target)
    {
        var sb = new System.Text.StringBuilder();
        var first = true;

        foreach (var provider in EnumerateActiveCaptureForceProviders())
        {
            if (!first) { sb.Append(", "); }
            first = false;
            var node = provider as Node3D;
            sb.Append($"{node?.Name ?? "?"}=force:{provider.GetForceFor(target).Length():F2}");
        }

        foreach (var provider in EnumerateActiveCaptureOffsetProviders())
        {
            if (!first) { sb.Append(", "); }
            first = false;
            var node = provider as Node3D;
            sb.Append($"{node?.Name ?? "?"}=offset:{provider.GetVelocityOffsetFor(target).Length():F2}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Aggregates only forces from providers that opt-in via
    /// <see cref="IForceProvider3D.IsCaptureForce"/> — the subset of forces that should
    /// count toward control-loss detection. Excludes baseline forces (gravity) so the
    /// actor can still be subject to gravity without being classified as "captured."
    /// </summary>
    public Vector3 GetCaptureForce(Node3D target)
    {
        var totalForce = Vector3.Zero;
        foreach (var provider in EnumerateActiveCaptureForceProviders())
        {
            totalForce += provider.GetForceFor(target);
        }
        return totalForce;
    }

    /// <summary>
    /// Aggregates only velocity offsets from providers that opt-in via
    /// <see cref="IVelocityOffsetProvider3D.IsCaptureOffset"/> — the subset of offsets
    /// that should count toward control-loss detection.
    /// </summary>
    public Vector3 GetCaptureVelocityOffset(Node3D target)
    {
        var totalOffset = Vector3.Zero;
        foreach (var provider in EnumerateActiveCaptureOffsetProviders())
        {
            totalOffset += provider.GetVelocityOffsetFor(target);
        }
        return totalOffset;
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
