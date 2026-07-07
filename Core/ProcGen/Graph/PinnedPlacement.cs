namespace Jmodot.Core.ProcGen.Graph;

using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///     A designer-authored forced placement: a node <see cref="Template" /> pinned at the spine
///     position resolved by its <see cref="Anchor" />. A plain data holder — its invariants
///     (non-null template that is an <see cref="INodeTemplate" />, present anchor resolving to an
///     in-range index) are enforced by the owning <see cref="ISkeletonConfig.Validate" />, not a
///     self-Validate.
/// </summary>
[GlobalClass, Tool]
public sealed partial class PinnedPlacement : Resource
{
    /// <summary>
    ///     The pinned node template. Exported as a base <see cref="Resource" /> because Godot cannot
    ///     export the <see cref="INodeTemplate" /> interface; the engine reads it through
    ///     <see cref="AsNodeTemplate" />. A PP <c>RoomTemplate</c> is the expected assignment.
    /// </summary>
    [Export] public Resource? Template { get; private set; }

    /// <summary>The anchor resolving this pin's spine index. Consumed polymorphically.</summary>
    [Export] public PinAnchor? Anchor { get; private set; }

    /// <summary>
    ///     REQUIRED rooms attached directly adjacent to the pinned node (set-piece flanks — "the
    ///     heart chamber with a key room on either side"). Each entry must be an
    ///     <see cref="INodeTemplate" /> (a PP <c>RoomTemplate</c>); the generator attaches every one
    ///     as a dead-end neighbor with <see cref="EdgeProvenanceKind.PinnedNeighbor" /> provenance,
    ///     and fails the floor as PinUnsatisfiable when the pinned template cannot host them all.
    /// </summary>
    [Export] public Godot.Collections.Array<Resource?> RequiredNeighbors { get; private set; } = new();

    /// <summary>
    ///     When true, the spine edge LEAVING the pinned node (toward the sink) is marked gated —
    ///     progression past the set-piece is blocked until its gate opens. Ignored when the pinned
    ///     node is the sink (there is no exit edge).
    /// </summary>
    [Export] public bool GateExitEdge { get; private set; }

    /// <summary><see cref="Template" /> viewed through the engine contract, or null if unset / not a template.</summary>
    public INodeTemplate? AsNodeTemplate => this.Template as INodeTemplate;

    /// <summary><see cref="RequiredNeighbors" /> projected through the engine contract; non-template / null slots dropped.</summary>
    public IReadOnlyList<INodeTemplate> RequiredNeighborTemplates =>
        this.RequiredNeighbors.OfType<INodeTemplate>().ToList();

    #region Test Helpers
#if TOOLS
    internal void SetTemplate(Resource? value) => this.Template = value;
    internal void SetAnchor(PinAnchor? value) => this.Anchor = value;
    internal void SetRequiredNeighbors(Godot.Collections.Array<Resource?> value) => this.RequiredNeighbors = value;
    internal void SetGateExitEdge(bool value) => this.GateExitEdge = value;
#endif
    #endregion
}
