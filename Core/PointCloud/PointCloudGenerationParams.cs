namespace Jmodot.Core.PointCloud;

/// <summary>
/// Parameters for point cloud shape generation.
/// Bundles distribution, spacing, jitter, count, Y-cutoff, and seed
/// to avoid parameter explosion on shape strategy Generate() methods.
/// </summary>
public readonly struct PointCloudGenerationParams
{
    /// <summary>
    /// Algorithm used to distribute points (PoissonDisk, Random, Uniform).
    /// </summary>
    public PointCloudDistribution Distribution { get; init; }

    /// <summary>
    /// Minimum distance between any two points.
    /// </summary>
    public float MinSpacing { get; init; }

    /// <summary>
    /// Random offset applied to each point (0 = none, 1 = full).
    /// </summary>
    public float PositionJitter { get; init; }

    /// <summary>
    /// Target number of points to generate.
    /// </summary>
    public int TargetCount { get; init; }

    /// <summary>
    /// Vertical lower cutoff as a normalized value (0.0 = bottom of shape, 0.5 = center).
    /// Maps to actual Y bounds during generation.
    /// </summary>
    public float YMinNormalized { get; init; }

    /// <summary>
    /// Vertical upper cutoff as a normalized value (1.0 = top of shape).
    /// Combined with YMinNormalized to define the vertical generation range.
    /// Default is 1.0 (full top).
    /// </summary>
    public float YMaxNormalized { get; init; }

    /// <summary>
    /// Returns true if Y bounds restrict the full shape (either YMin > 0 or YMax &lt; 1).
    /// </summary>
    public bool HasYCutoff => YMinNormalized > 0f || YMaxNormalized < 1f;

    /// <summary>
    /// When true, all points are generated on the XY plane (Z=0).
    /// Shapes project to their 2D equivalents: Sphere→Circle, Box→Rectangle, Ellipsoid→Ellipse.
    /// Used for orthographic/2D-style games where depth is not meaningful.
    /// </summary>
    public bool FlattenToPlane { get; init; }

    /// <summary>
    /// Random seed for deterministic generation.
    /// </summary>
    public int Seed { get; init; }
}
