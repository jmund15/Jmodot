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
    /// Random offset applied to each point position after generation.
    /// 0 = no jitter (deterministic positions), 1 = full jitter (very chaotic).
    /// Applied as a fraction of MinSpacing.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float PositionJitter { get; set; } = 0.2f;

    /// <summary>
    /// Density of particles per unit area (2D) or volume (3D).
    /// Combined with the shape's computed area/volume to determine point count.
    /// Higher values = more particles. Typical range: 0.1 (sparse) to 2.0 (dense).
    /// </summary>
    [Export(PropertyHint.Range, "0.05, 5.0, 0.05")]
    public float ParticleDensity { get; set; } = 0.5f;

    /// <summary>
    /// Maximum number of points that can be generated, regardless of density.
    /// Acts as a performance safety cap.
    /// </summary>
    [Export(PropertyHint.Range, "1, 200, 1")]
    public int MaxPointCount { get; set; } = 100;

    /// <summary>
    /// Computes the target point count from density and spatial metric, clamped to [1, maxCount].
    /// </summary>
    public static int ResolveTargetCount(float density, float volumeOrArea, int maxCount)
    {
        int computed = (int)(density * volumeOrArea);
        return Mathf.Clamp(computed, 1, maxCount);
    }

    /// <summary>
    /// Vertical cutoff as a normalized value.
    /// 0.0 = full shape (default), 0.5 = hemisphere (upper half only).
    /// Used for ground explosions to prevent particles below ground plane.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 0.9, 0.05")]
    public float YMinNormalized { get; set; } = 0.0f;

    /// <summary>
    /// When true, generates points on the XY plane (Z=0) instead of full 3D volume.
    /// Shapes project to their 2D equivalents (Sphere→Circle, Box→Rectangle).
    /// Useful for orthographic/2D-style games where depth is not meaningful.
    /// </summary>
    [Export]
    public bool FlattenToPlane { get; set; } = false;

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
            PositionJitter = 0.2f,
            ParticleDensity = 0.5f,
            MaxPointCount = 100
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
            PositionJitter = 0.15f,
            ParticleDensity = 1.5f,
            MaxPointCount = 150
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
            PositionJitter = 0.3f,
            ParticleDensity = 0.2f,
            MaxPointCount = 60
        };
    }
}
