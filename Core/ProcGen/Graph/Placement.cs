namespace Jmodot.Core.ProcGen.Graph;

using Jmodot.Implementation.ProcGen.Graph;

/// <summary>
///     A candidate room attachment under evaluation by the graph generator: the open
///     <see cref="CandidateSlot" /> being filled and the <see cref="INodeTemplate" /> proposed to
///     fill it. Passed by <c>in</c> to placement rules so the generator can evaluate many rules
///     per candidate without copying. A data-only carrier — no behavior.
/// </summary>
public readonly struct Placement
{
    public Placement(CandidateSlot slot, INodeTemplate template)
    {
        this.Slot = slot;
        this.Template = template;
    }

    /// <summary>The open slot (port or edge-split) this placement would attach to.</summary>
    public CandidateSlot Slot { get; }

    /// <summary>The template proposed for the new node attaching at <see cref="Slot" />.</summary>
    public INodeTemplate Template { get; }
}
