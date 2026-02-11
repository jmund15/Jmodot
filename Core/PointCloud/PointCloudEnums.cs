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
