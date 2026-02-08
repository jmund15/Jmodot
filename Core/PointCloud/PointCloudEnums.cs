namespace Jmodot.Core.PointCloud;

/// <summary>
/// Algorithm used to distribute points within a volume.
/// </summary>
public enum PointCloudDistribution
{
    /// <summary>
    /// Points placed on a regular grid within the bounds.
    /// Predictable, evenly spaced, but can look artificial.
    /// </summary>
    Uniform,

    /// <summary>
    /// Points placed completely randomly within the bounds.
    /// Fast but can result in clustering or gaps.
    /// </summary>
    Random,

    /// <summary>
    /// Points distributed using Poisson disk sampling.
    /// Ensures minimum spacing between points while appearing natural.
    /// Best balance of coverage and organic feel.
    /// </summary>
    PoissonDisk
}

/// <summary>
/// The base shape used to define point cloud boundaries.
/// </summary>
public enum PointCloudShape
{
    /// <summary>
    /// Uniform sphere (all radii equal).
    /// </summary>
    Sphere,

    /// <summary>
    /// Ellipsoid with potentially different X, Y, Z radii.
    /// A sphere is a special case where all radii are equal.
    /// </summary>
    Ellipsoid,

    /// <summary>
    /// Axis-aligned box.
    /// </summary>
    Box,

    /// <summary>
    /// Vertical cylinder with configurable radius and height.
    /// </summary>
    Cylinder
}
