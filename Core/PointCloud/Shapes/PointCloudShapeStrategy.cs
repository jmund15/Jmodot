namespace Jmodot.Core.PointCloud.Shapes;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Abstract base for point cloud shape strategies.
/// Each concrete shape defines how points are generated within its volume
/// and how to test containment. Exported as a Godot Resource for data-driven configuration.
/// </summary>
[GlobalClass]
public abstract partial class PointCloudShapeStrategy : Resource
{
    /// <summary>
    /// Generates a list of points distributed within this shape's volume.
    /// </summary>
    /// <param name="scale">Scale/radii of the shape (interpretation varies by shape type).</param>
    /// <param name="genParams">Generation parameters (distribution, spacing, count, etc.).</param>
    /// <returns>List of 3D positions within the volume.</returns>
    public abstract List<Vector3> Generate(Vector3 scale, PointCloudGenerationParams genParams);

    /// <summary>
    /// Tests whether a point lies inside or on the surface of this shape.
    /// </summary>
    /// <param name="point">Point to test.</param>
    /// <param name="scale">Scale/radii of the shape.</param>
    public abstract bool IsInside(Vector3 point, Vector3 scale);

    /// <summary>
    /// Computes the 3D volume of this shape at the given scale.
    /// Used by density-based point count resolution when FlattenToPlane is false.
    /// </summary>
    public abstract float ComputeVolume(Vector3 scale);

    /// <summary>
    /// Computes the 2D projected area (XY footprint) of this shape at the given scale.
    /// Used by density-based point count resolution when FlattenToPlane is true.
    /// </summary>
    public abstract float ComputeArea(Vector3 scale);
}
