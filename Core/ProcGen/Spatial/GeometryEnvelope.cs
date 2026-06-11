namespace Jmodot.Core.ProcGen.Spatial;

using Godot;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The floor-level bounding envelope the embedder fills — a pipeline parameter (P3b.5), never a
///     per-template footprint (template size travels as <see cref="ISpatialNodeTemplate.FootprintCells" />).
///     This namespace is deliberately exempt from Jmodot's 2D/3D parity convention — dungeon-floor
///     embedding is grid-3D only, with no 2D mirror.
/// </summary>
[GlobalClass, Tool]
public sealed partial class GeometryEnvelope : Resource
{
    /// <summary>The envelope extent in cells. Default <see cref="Vector3I.Zero" /> is the invalid-at-zero sentinel; <see cref="Validate" /> rejects any non-positive dimension.</summary>
    [Export] public Vector3I SizeCells { get; private set; }

    /// <summary>Fail-fast: throws if any dimension is &lt;= 0 (an envelope must bound a positive volume).</summary>
    public void Validate()
    {
        if (this.SizeCells.X <= 0 || this.SizeCells.Y <= 0 || this.SizeCells.Z <= 0)
        {
            throw new ResourceConfigurationException(
                $"{nameof(GeometryEnvelope)}.{nameof(this.SizeCells)} must have all dimensions > 0 (got {this.SizeCells}); a zero/negative extent cannot bound a floor.", this);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetSizeCells(Vector3I value) => this.SizeCells = value;
#endif
    #endregion
}
