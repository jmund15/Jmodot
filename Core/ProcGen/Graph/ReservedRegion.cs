namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     A grid-aligned region the realizer has reserved for a node. Engine-owned and <b>scene-free</b>:
///     positions are exact-float but integer-valued by contract (grid coordinates stored as floats).
///     <see cref="Token" /> is an <b>opaque handle the engine never dereferences</b> — the
///     Token→scene mapping belongs to the P3b realizer and is consumed post-generation by PP. Keeping
///     this type scene-free is the BOUNDARY invariant that lets the whole generator stay pure-Logic.
/// </summary>
public readonly struct ReservedRegion
{
    public ReservedRegion(Transform3D transform, Aabb bounds, StringName token)
    {
        this.Transform = transform;
        this.Bounds = bounds;
        this.Token = token;
    }

    /// <summary>Placement transform (grid-aligned origin + orientation) of the reserved region.</summary>
    public Transform3D Transform { get; }

    /// <summary>Axis-aligned footprint extent of the reserved region.</summary>
    public Aabb Bounds { get; }

    /// <summary>Opaque realizer handle; never dereferenced by the engine. Resolved to a scene by PP post-gen.</summary>
    public StringName Token { get; }
}
