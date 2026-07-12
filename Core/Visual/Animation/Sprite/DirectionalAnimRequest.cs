namespace Jmodot.Core.Visual.Animation.Sprite;

using System.Collections.Generic;
using Godot;

/// <summary>
/// A resolved directional animation request the orchestrator fans out to a composite's slaves.
/// Carries the base state name, the resolved direction label + world vector, and the label map +
/// separator so each slave can independently resolve its own concrete clip via
/// <c>DirectionalClipResolver</c> under its own <see cref="SlotFallbackPolicy"/>.
/// </summary>
public readonly struct DirectionalAnimRequest
{
    public StringName BaseName { get; }

    /// <summary>The current direction label (e.g. "downLeft"); empty for an undirected request.</summary>
    public string DirectionLabel { get; }

    /// <summary>Current facing as a world vector; <see cref="Vector3.Zero"/> disables nearest-directional degradation.</summary>
    public Vector3 Direction { get; }

    public IReadOnlyDictionary<Vector3, string> DirectionLabels { get; }

    public string Separator { get; }

    public DirectionalAnimRequest(
        StringName baseName,
        string directionLabel,
        Vector3 direction,
        IReadOnlyDictionary<Vector3, string> directionLabels,
        string separator)
    {
        BaseName = baseName;
        DirectionLabel = directionLabel;
        Direction = direction;
        DirectionLabels = directionLabels;
        Separator = separator;
    }
}
