namespace Jmodot.Implementation.ProcGen.Graph;

using Godot;
using Jmodot.Core.ProcGen.Graph;

/// <summary>
///     A placement candidate the generator enumerates over a <see cref="PartialGraph" /> — either an
///     open port on a node (<see cref="PortSlot" />) or an existing edge that can be split
///     (<see cref="EdgeSplitSlot" />). Plain CLR (no Godot base): constructed and discarded during
///     generation, never .tres-authored.
///     <para>
///         Value-equality is by the content-derived <see cref="Id" /> — never reference identity — so a
///         slot re-enumerated for the same topology compares equal across distinct CLR instances, which
///         keeps placement deterministic. <see cref="Id" /> is never derived from a hash, instance id,
///         or guid.
///     </para>
/// </summary>
public abstract class CandidateSlot
{
    // ASCII unit separator U+001F, the same delimiter GraphSignature uses for its FieldSep. Authored
    // node ids, template ids, and port names never contain it, so id content can never forge a segment
    // boundary nor let the two slot kinds collide.
    protected const char Sep = (char)0x1F;

    protected CandidateSlot(StringName id)
    {
        this.Id = id;
    }

    /// <summary>Content-derived identity; basis of value-equality. Never reference- or hash-derived.</summary>
    public StringName Id { get; }

    /// <summary>The graph node this candidate is anchored to (a port's owner, or an edge's From).</summary>
    public abstract IGraphNode AnchorNode { get; }

    public override bool Equals(object? obj) => obj is CandidateSlot other && this.Id == other.Id;

    public override int GetHashCode() => this.Id.GetHashCode();
}
