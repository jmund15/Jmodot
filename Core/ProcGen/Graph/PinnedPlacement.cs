namespace Jmodot.Core.ProcGen.Graph;

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

    /// <summary><see cref="Template" /> viewed through the engine contract, or null if unset / not a template.</summary>
    public INodeTemplate? AsNodeTemplate => this.Template as INodeTemplate;

    #region Test Helpers
#if TOOLS
    internal void SetTemplate(Resource? value) => this.Template = value;
    internal void SetAnchor(PinAnchor? value) => this.Anchor = value;
#endif
    #endregion
}
