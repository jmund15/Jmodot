namespace Jmodot.Core.Visual;

using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Project-wide 2.5D projection constants for tilted-camera sprite facing. A consuming game's
/// active camera pushes its derived values here at startup (framework-agnostic seam; mirrors
/// <c>CombatFactoryDefaults</c> / <c>CollisionDefaults</c> — Jmodot never learns what a camera is).
/// The seam is owner-scoped: a camera <see cref="Publish(object, float)"/>es itself as the publisher
/// and only that same owner's <see cref="Retract(object)"/> un-publishes.
/// </summary>
/// <remarks>
/// Owner-scoping is load-bearing, not defensive. A scene swap adds the incoming scene (and its
/// camera publishes) BEFORE the outgoing scene's deferred free fires its camera's exit — so an
/// ownerless retract un-publishes a projection a LIVE camera owns, and nothing republishes for the
/// rest of the session. Every <see cref="IsPublished"/>-gated consumer then silently falls back to
/// its authored basis with no error raised.
/// </remarks>
public static class VisualProjectionDefaults
{
    private static float _depthForeshorten = 1f;

    // Identity of the camera that published the live projection; null when unpublished. The
    // ownerless DepthForeshorten setter has no camera to name, so it claims this stand-in.
    private static readonly object LegacyPublisher = new();
    private static object? _publisher;

    /// <summary>
    /// Screen foreshortening of world depth (Z) relative to world horizontal (X):
    /// = sin(camera pitch from horizontal). <c>1.0</c> = top-down / no foreshortening (the identity
    /// default, so an unconfigured consumer keeps pre-seam behavior — no silent regression).
    /// </summary>
    /// <remarks>
    /// Ownerless legacy path: it publishes, but its writer cannot be identified, so the projection
    /// it leaves can only be withdrawn by the ownerless <see cref="Retract()"/>. Cameras must use
    /// <see cref="Publish(object, float)"/>. Retained for test setup and any consumer that has no
    /// stable identity to offer.
    /// </remarks>
    public static float DepthForeshorten
    {
        get => _depthForeshorten;
        set
        {
            if (IsPublished && !Mathf.IsEqualApprox(value, _depthForeshorten))
            {
                JmoLogger.Warning(nameof(VisualProjectionDefaults),
                    $"Projection foreshorten {_depthForeshorten:F4} is already published; a second " +
                    $"active camera is overwriting it with {value:F4}. Last writer wins — iso sprite " +
                    "facing follows the most recent publisher. Expected one active camera per scene.");
            }

            _depthForeshorten = value;
            _publisher = LegacyPublisher;
            IsPublished = true;
        }
    }

    /// <summary>
    /// Whether an active camera has actually published a projection. Consumers that OVERWRITE
    /// scene state (rather than merely reading a factor) must gate on this: the identity default
    /// is indistinguishable from a genuine top-down camera by value alone, and acting on it
    /// applies a 90° frame that lays sprite planes edge-on — invisible, with no error.
    /// </summary>
    public static bool IsPublished { get; private set; }

    /// <summary>
    /// Un-publish unconditionally, restoring the identity default. Ownerless counterpart to the
    /// <see cref="DepthForeshorten"/> setter; cameras must use <see cref="Retract(object)"/>.
    /// </summary>
    public static void Retract()
    {
        _depthForeshorten = 1f;
        _publisher = null;
        IsPublished = false;
    }

    /// <summary>
    /// Publish <paramref name="depthForeshorten"/> and claim the seam for <paramref name="owner"/>.
    /// Warns when a DIFFERENT owner is already publishing — two live cameras is a contract
    /// violation regardless of whether their tilts happen to agree, and same-tilt overlap is
    /// exactly the case a value comparison cannot see.
    /// </summary>
    public static void Publish(object owner, float depthForeshorten)
    {
        if (IsPublished && !ReferenceEquals(owner, _publisher))
        {
            JmoLogger.Warning(nameof(VisualProjectionDefaults),
                $"Projection foreshorten {_depthForeshorten:F4} is already published by another " +
                $"active camera; {owner.GetType().Name} is claiming the seam with " +
                $"{depthForeshorten:F4}. Last publisher wins — iso sprite facing follows it. " +
                "Expected one active camera per scene.");
        }

        _depthForeshorten = depthForeshorten;
        _publisher = owner;
        IsPublished = true;
    }

    /// <summary>
    /// Un-publish on behalf of <paramref name="owner"/> — a no-op unless it is the current
    /// publisher, so a camera that has already been superseded cannot withdraw the live projection
    /// on its way out of the tree.
    /// </summary>
    public static void Retract(object owner)
    {
        if (!ReferenceEquals(owner, _publisher))
        {
            return;
        }

        Retract();
    }

    /// <summary>Restore the identity default. Call in test teardown to avoid cross-suite leakage.</summary>
    public static void Reset() => Retract();
}
