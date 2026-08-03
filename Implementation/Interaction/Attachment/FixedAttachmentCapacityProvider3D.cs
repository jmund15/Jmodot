namespace Jmodot.Implementation.Interaction.Attachment;

using Godot;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Visual;

/// <summary>
/// Capacity authored as a flat number, independent of the host's art and stats. The right rule
/// whenever "how many riders fit" is part of the host's identity rather than a consequence of how
/// big it happens to look.
/// </summary>
[GlobalClass, Tool]
public partial class FixedAttachmentCapacityProvider3D : AttachmentCapacityProvider3D
{
    /// <summary>Total rider footprint this host carries. 0 refuses every rider.</summary>
    [Export] public float Capacity { get; private set; } = 1f;

    public override float GetCapacity(VisualBounds3D bounds, IStatProvider? stats, Node? owner)
    {
        return Mathf.Max(this.Capacity, 0f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetCapacity(float capacity) => this.Capacity = capacity;
#endif
    #endregion
}
