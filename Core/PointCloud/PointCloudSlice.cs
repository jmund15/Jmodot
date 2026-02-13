namespace Jmodot.Core.PointCloud;

/// <summary>
/// Defines a vertical slice within a point cloud volume.
/// Used for partitioning 3D shapes into horizontal tiers (e.g., bottom, mid, top).
/// Y values are normalized (0-1) and get scaled to actual bounds during generation.
/// </summary>
public readonly struct PointCloudSlice
{
    /// <summary>
    /// Lower Y boundary as a normalized value (0.0 = bottom, 1.0 = top).
    /// </summary>
    public float YMin { get; init; }

    /// <summary>
    /// Upper Y boundary as a normalized value (0.0 = bottom, 1.0 = top).
    /// </summary>
    public float YMax { get; init; }

    /// <summary>
    /// Multiplier for point density within this slice.
    /// 1.0 = standard density, 2.0 = double density, 0.5 = half density.
    /// </summary>
    public float DensityMultiplier { get; init; }

    /// <summary>
    /// Creates a new slice with the specified Y range and density.
    /// </summary>
    public PointCloudSlice(float yMin, float yMax, float densityMultiplier = 1.0f)
    {
        YMin = yMin;
        YMax = yMax;
        DensityMultiplier = densityMultiplier;
    }

    /// <summary>
    /// Calculates the height of this slice as a fraction of the total height.
    /// </summary>
    public float Height => YMax - YMin;

    /// <summary>
    /// Checks if a normalized Y value falls within this slice.
    /// </summary>
    public bool ContainsY(float normalizedY) => normalizedY >= YMin && normalizedY <= YMax;

    /// <summary>
    /// Standard slice covering the full vertical range with standard density.
    /// </summary>
    public static PointCloudSlice Full => new(0f, 1f, 1f);

    private const float OneThird = 1f / 3f;
    private const float TwoThirds = 2f / 3f;

    /// <summary>
    /// Bottom third of the volume.
    /// </summary>
    public static PointCloudSlice BottomThird => new(0f, OneThird, 1f);

    /// <summary>
    /// Middle third of the volume.
    /// </summary>
    public static PointCloudSlice MiddleThird => new(OneThird, TwoThirds, 1f);

    /// <summary>
    /// Top third of the volume.
    /// </summary>
    public static PointCloudSlice TopThird => new(TwoThirds, 1f, 1f);
}
