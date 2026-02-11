namespace Jmodot.Core.PointCloud;

using Godot;
using Jmodot.Core.PointCloud.Shapes;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Configuration resource for point cloud generation.
/// Defines the distribution algorithm, shape strategy, spacing, and density parameters.
/// Reusable across different systems (explosions, AoE spawns, procedural placement).
/// </summary>
[GlobalClass]
public partial class PointCloudConfig : Resource
{
    /// <summary>
    /// Algorithm used to distribute points within the volume.
    /// PoissonDisk provides the best balance of coverage and natural appearance.
    /// </summary>
    [Export]
    public PointCloudDistribution Distribution { get; set; } = PointCloudDistribution.PoissonDisk;

    /// <summary>
    /// Shape strategy that defines the point cloud boundaries and generation algorithm.
    /// Each shape type (Sphere, Ellipsoid, Box, Cylinder) is a separate Resource.
    /// </summary>
    [Export, RequiredExport]
    public PointCloudShapeStrategy ShapeStrategy { get; set; } = null!;

    /// <summary>
    /// Minimum distance between any two points.
    /// Used by PoissonDisk sampling. Lower values = denser clouds.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    public float MinSpacing { get; set; } = 0.3f;

    /// <summary>
    /// Reserved for future use. Currently unused by generation algorithms.
    /// </summary>
    [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
    public float MaxSpacing { get; set; } = 0.8f;

    /// <summary>
    /// Random offset applied to each point position after generation.
    /// 0 = no jitter (deterministic positions), 1 = full jitter (very chaotic).
    /// Applied as a fraction of MinSpacing.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float PositionJitter { get; set; } = 0.2f;

    /// <summary>
    /// Target number of points to generate (approximate).
    /// Actual count may vary based on spacing constraints.
    /// </summary>
    [Export(PropertyHint.Range, "1, 100, 1")]
    public int TargetPointCount { get; set; } = 20;

    /// <summary>
    /// Vertical cutoff as a normalized value.
    /// 0.0 = full shape (default), 0.5 = hemisphere (upper half only).
    /// Used for ground explosions to prevent particles below ground plane.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 0.9, 0.05")]
    public float YMinNormalized { get; set; } = 0.0f;

    /// <summary>
    /// Creates a default configuration suitable for medium explosions.
    /// </summary>
    public static PointCloudConfig CreateDefault()
    {
        return new PointCloudConfig
        {
            Distribution = PointCloudDistribution.PoissonDisk,
            ShapeStrategy = new SphereCloudShape(),
            MinSpacing = 0.3f,
            MaxSpacing = 0.8f,
            PositionJitter = 0.2f,
            TargetPointCount = 20
        };
    }

    /// <summary>
    /// Creates a dense configuration for smaller, more detailed effects.
    /// </summary>
    public static PointCloudConfig CreateDense()
    {
        return new PointCloudConfig
        {
            Distribution = PointCloudDistribution.PoissonDisk,
            ShapeStrategy = new SphereCloudShape(),
            MinSpacing = 0.15f,
            MaxSpacing = 0.4f,
            PositionJitter = 0.15f,
            TargetPointCount = 40
        };
    }

    /// <summary>
    /// Creates a sparse configuration for larger, more spread out effects.
    /// </summary>
    public static PointCloudConfig CreateSparse()
    {
        return new PointCloudConfig
        {
            Distribution = PointCloudDistribution.PoissonDisk,
            ShapeStrategy = new SphereCloudShape(),
            MinSpacing = 0.5f,
            MaxSpacing = 1.2f,
            PositionJitter = 0.3f,
            TargetPointCount = 12
        };
    }
}
