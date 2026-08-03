namespace Jmodot.Implementation.Interaction.Attachment;

using Godot;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Visual;

/// <summary>
/// Capacity derived from the host's measured silhouette, so a bigger body carries more riders
/// without a second authored number to keep in sync. Planar by construction — the silhouette's
/// depth is structurally zero, so area is width × height.
/// </summary>
[GlobalClass, Tool]
public partial class BoundsDerivedAttachmentCapacityProvider3D : AttachmentCapacityProvider3D
{
    /// <summary>Rider footprint carried per square metre of measured silhouette.</summary>
    [Export] public float CapacityPerSquareMeter { get; private set; } = 4f;

    /// <summary>
    /// An unmeasured host answers 0 rather than falling back to a constant: a silhouette that could
    /// not be measured is a configuration fault, and inventing capacity would hide it behind riders
    /// that attach to nothing.
    /// </summary>
    public override float GetCapacity(VisualBounds3D bounds, IStatProvider? stats, Node? owner)
    {
        if (!bounds.IsMeasured) { return 0f; }

        var area = Mathf.Max(bounds.Width, 0f) * Mathf.Max(bounds.Height, 0f);
        return area * Mathf.Max(this.CapacityPerSquareMeter, 0f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetCapacityPerSquareMeter(float density) => this.CapacityPerSquareMeter = density;
#endif
    #endregion
}
