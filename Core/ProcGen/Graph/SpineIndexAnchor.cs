namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     The MVP <see cref="PinAnchor" />: pins to an explicit, designer-authored spine index.
///     <see cref="ResolveSpineIndex" /> returns <see cref="Index" /> verbatim, ignoring the config
///     context (a fixed positional pin).
/// </summary>
[GlobalClass, Tool]
public sealed partial class SpineIndexAnchor : PinAnchor
{
    /// <summary>Zero-based spine index this pin targets. Range-checked against the spine length by config validation.</summary>
    [Export] public int Index { get; private set; }

    public override int ResolveSpineIndex(ISkeletonConfig config) => this.Index;

    #region Test Helpers
#if TOOLS
    internal void SetIndex(int value) => this.Index = value;
#endif
    #endregion
}
