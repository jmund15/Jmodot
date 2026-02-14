namespace Jmodot.Core.PointCloud;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.PointCloud.Shapes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Static utility class for point cloud generation.
/// Provides the main GeneratePoints() facade and shared geometry/algorithm utilities
/// used by shape strategies.
/// </summary>
public static class PointCloudGenerator
{
    private const int MaxRejectionIterations = 1000;

    #region Main API

    /// <summary>
    /// Generates a list of points distributed within a volume defined by the config.
    /// Delegates to the shape strategy on the config (or an optional override).
    /// <para>
    /// Point count resolution priority:
    /// 1. <paramref name="targetCountOverride"/> — explicit caller override (highest priority).
    /// 2. <see cref="PointCloudConfig.ParticleDensity"/> × shape area/volume — auto-computed from config.
    ///    Uses <see cref="PointCloudShapeStrategy.ComputeArea"/> when <see cref="PointCloudConfig.FlattenToPlane"/>
    ///    is true, <see cref="PointCloudShapeStrategy.ComputeVolume"/> otherwise.
    /// </para>
    /// </summary>
    /// <param name="config">Configuration for distribution algorithm, shape, spacing, etc.</param>
    /// <param name="scale">Scale/radii of the shape (interpreted by the shape strategy).</param>
    /// <param name="seed">Random seed for deterministic generation.</param>
    /// <param name="targetCountOverride">Optional explicit point count (highest priority, bypasses density).</param>
    /// <param name="shapeOverride">Optional shape strategy override (e.g., auto-resolved from collision shape).</param>
    /// <param name="yMinOverride">Optional Y-cutoff override (e.g., auto-ground-clip).</param>
    /// <returns>List of 3D positions within the specified volume.</returns>
    public static List<Vector3> GeneratePoints(
        PointCloudConfig config,
        Vector3 scale,
        int seed,
        int? targetCountOverride = null,
        PointCloudShapeStrategy? shapeOverride = null,
        float? yMinOverride = null)
    {
        var shape = shapeOverride ?? config.ShapeStrategy;
        if (shape == null)
        {
            JmoLogger.Error(typeof(PointCloudGenerator), "No shape strategy configured on PointCloudConfig.");
            return new List<Vector3>();
        }

        int targetCount;
        if (targetCountOverride.HasValue)
        {
            targetCount = targetCountOverride.Value;
        }
        else
        {
            float metric = config.FlattenToPlane
                ? shape.ComputeArea(scale)
                : shape.ComputeVolume(scale);
            targetCount = PointCloudConfig.ResolveTargetCount(config.ParticleDensity, metric, config.MaxPointCount);
        }

        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = targetCount,
            YMinNormalized = yMinOverride ?? config.YMinNormalized,
            YMaxNormalized = 1.0f,
            FlattenToPlane = config.FlattenToPlane,
            Seed = seed
        };

        return shape.Generate(scale, genParams);
    }

    /// <summary>
    /// Convenience method: generates points within a sphere.
    /// Delegates to SphereCloudShape internally.
    /// </summary>
    public static List<Vector3> GenerateInSphere(float radius, PointCloudConfig config, int targetCount, int seed)
    {
        var shape = new SphereCloudShape();
        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = targetCount,
            YMaxNormalized = 1.0f,
            FlattenToPlane = config.FlattenToPlane,
            Seed = seed
        };
        return shape.Generate(new Vector3(radius, radius, radius), genParams);
    }

    /// <summary>
    /// Convenience method: generates points within an ellipsoid.
    /// Delegates to EllipsoidCloudShape internally.
    /// </summary>
    public static List<Vector3> GenerateInEllipsoid(Vector3 radii, PointCloudConfig config, int targetCount, int seed)
    {
        var shape = new EllipsoidCloudShape();
        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = targetCount,
            YMaxNormalized = 1.0f,
            FlattenToPlane = config.FlattenToPlane,
            Seed = seed
        };
        return shape.Generate(radii, genParams);
    }

    /// <summary>
    /// Generates points within a vertical slice of a sphere.
    /// Delegates to SphereCloudShape with Y-cutoff parameters.
    /// </summary>
    public static List<Vector3> GenerateInSlice(float radius, PointCloudConfig config, PointCloudSlice slice, int seed)
    {
        var sphereScale = new Vector3(radius, radius, radius);
        var sphere = new SphereCloudShape();
        float metric = config.FlattenToPlane
            ? sphere.ComputeArea(sphereScale)
            : sphere.ComputeVolume(sphereScale);
        int baseCount = PointCloudConfig.ResolveTargetCount(config.ParticleDensity, metric, config.MaxPointCount);
        int adjustedTarget = Math.Max(1, (int)(baseCount * slice.Height * slice.DensityMultiplier));

        var shape = new SphereCloudShape();
        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = adjustedTarget,
            YMinNormalized = slice.YMin,
            YMaxNormalized = slice.YMax,
            FlattenToPlane = config.FlattenToPlane,
            Seed = seed
        };
        return shape.Generate(new Vector3(radius, radius, radius), genParams);
    }

    #endregion

    #region Geometry Utilities

    /// <summary>
    /// Checks if a point lies inside or on the surface of a sphere.
    /// </summary>
    public static bool IsInsideSphere(Vector3 point, float radius)
    {
        return point.LengthSquared() <= radius * radius;
    }

    /// <summary>
    /// Checks if a point lies inside or on the surface of an ellipsoid.
    /// Uses the ellipsoid equation: (x/a)² + (y/b)² + (z/c)² <= 1
    /// </summary>
    public static bool IsInsideEllipsoid(Vector3 point, Vector3 radii)
    {
        float nx = point.X / radii.X;
        float ny = point.Y / radii.Y;
        float nz = point.Z / radii.Z;
        return (nx * nx + ny * ny + nz * nz) <= 1.0f;
    }

    /// <summary>
    /// Generates a random point uniformly distributed inside a sphere.
    /// Uses rejection sampling for uniform distribution.
    /// </summary>
    public static Vector3 GenerateRandomPointInSphere(float radius, Random rng)
    {
        for (int i = 0; i < MaxRejectionIterations; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * radius;
            float y = (float)(rng.NextDouble() * 2 - 1) * radius;
            float z = (float)(rng.NextDouble() * 2 - 1) * radius;
            var point = new Vector3(x, y, z);

            if (IsInsideSphere(point, radius))
            {
                return point;
            }
        }

        JmoLogger.Warning(typeof(PointCloudGenerator),
            $"Rejection sampling failed after {MaxRejectionIterations} iterations, returning origin");
        return Vector3.Zero;
    }

    /// <summary>
    /// Generates a random point uniformly distributed inside an ellipsoid.
    /// </summary>
    public static Vector3 GenerateRandomPointInEllipsoid(Vector3 radii, Random rng)
    {
        var unitPoint = GenerateRandomPointInSphere(1.0f, rng);
        return new Vector3(unitPoint.X * radii.X, unitPoint.Y * radii.Y, unitPoint.Z * radii.Z);
    }

    /// <summary>
    /// Applies random jitter to a point position.
    /// </summary>
    public static Vector3 ApplyJitter(Vector3 point, float jitterAmount, float spacing, Random rng)
    {
        if (jitterAmount <= 0f) { return point; }

        float maxJitter = spacing * jitterAmount;
        float jx = (float)(rng.NextDouble() * 2 - 1) * maxJitter;
        float jy = (float)(rng.NextDouble() * 2 - 1) * maxJitter;
        float jz = (float)(rng.NextDouble() * 2 - 1) * maxJitter;

        return point + new Vector3(jx, jy, jz);
    }

    /// <summary>
    /// Checks if a point lies inside or on a circle (XY plane, Z ignored).
    /// </summary>
    public static bool IsInsideCircle(Vector3 point, float radius)
    {
        return point.X * point.X + point.Y * point.Y <= radius * radius;
    }

    /// <summary>
    /// Checks if a point lies inside or on an ellipse (XY plane, Z ignored).
    /// Uses the 2D ellipse equation: (x/a)² + (y/b)² &lt;= 1
    /// </summary>
    public static bool IsInsideEllipse(Vector3 point, float radiusX, float radiusY)
    {
        float nx = point.X / radiusX;
        float ny = point.Y / radiusY;
        return nx * nx + ny * ny <= 1.0f;
    }

    /// <summary>
    /// Generates a random point uniformly distributed inside a circle (XY plane, Z=0).
    /// Uses rejection sampling for uniform distribution.
    /// </summary>
    public static Vector3 GenerateRandomPointInCircle(float radius, Random rng)
    {
        for (int i = 0; i < MaxRejectionIterations; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * radius;
            float y = (float)(rng.NextDouble() * 2 - 1) * radius;
            var point = new Vector3(x, y, 0);

            if (IsInsideCircle(point, radius))
            {
                return point;
            }
        }

        JmoLogger.Warning(typeof(PointCloudGenerator),
            $"Circle rejection sampling failed after {MaxRejectionIterations} iterations, returning origin");
        return Vector3.Zero;
    }

    /// <summary>
    /// Generates a random point uniformly distributed inside an ellipse (XY plane, Z=0).
    /// </summary>
    public static Vector3 GenerateRandomPointInEllipse(float radiusX, float radiusY, Random rng)
    {
        var unitPoint = GenerateRandomPointInCircle(1.0f, rng);
        return new Vector3(unitPoint.X * radiusX, unitPoint.Y * radiusY, 0);
    }

    /// <summary>
    /// Applies random jitter to a point position on the XY plane only (Z unchanged).
    /// Used when FlattenToPlane is active to prevent Z drift from jitter.
    /// </summary>
    public static Vector3 ApplyJitter2D(Vector3 point, float jitterAmount, float spacing, Random rng)
    {
        if (jitterAmount <= 0f) { return point; }

        float maxJitter = spacing * jitterAmount;
        float jx = (float)(rng.NextDouble() * 2 - 1) * maxJitter;
        float jy = (float)(rng.NextDouble() * 2 - 1) * maxJitter;

        return point + new Vector3(jx, jy, 0);
    }

    /// <summary>
    /// Calculates the minimum distance from a point to any point in a list.
    /// Returns float.MaxValue if the list is empty.
    /// </summary>
    public static float CalculateMinDistance(Vector3 point, List<Vector3> existingPoints)
    {
        if (existingPoints.Count == 0) { return float.MaxValue; }

        float minDistSq = float.MaxValue;
        foreach (var existing in existingPoints)
        {
            float distSq = point.DistanceSquaredTo(existing);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
            }
        }
        return Mathf.Sqrt(minDistSq);
    }

    #endregion

    #region Generic Algorithms

    /// <summary>
    /// Generic Poisson disk sampling with customizable candidate generation and bounds checking.
    /// Used by shape strategies to implement Poisson distribution without duplicating the algorithm.
    /// </summary>
    /// <param name="flattenZ">When true, uses 2D jitter (XY only) to prevent Z drift in flat mode.</param>
    public static List<Vector3> GeneratePoissonGeneric(
        Func<Random, Vector3> generateCandidate,
        Func<Vector3, bool> isInBounds,
        float minSpacing,
        int targetCount,
        float jitter,
        Random rng,
        bool flattenZ = false)
    {
        var points = new List<Vector3>();
        var grid = new SpatialHashGrid3D(minSpacing);
        int maxAttempts = targetCount * 30;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            var candidate = generateCandidate(rng);

            if (!isInBounds(candidate)) { continue; }

            if (!grid.HasNeighborWithinDistance(candidate, minSpacing))
            {
                var jitteredPoint = flattenZ
                    ? ApplyJitter2D(candidate, jitter, minSpacing, rng)
                    : ApplyJitter(candidate, jitter, minSpacing, rng);
                if (isInBounds(jitteredPoint))
                {
                    if (!grid.HasNeighborWithinDistance(jitteredPoint, minSpacing))
                    {
                        grid.Insert(jitteredPoint);
                        points.Add(jitteredPoint);
                    }
                    else
                    {
                        grid.Insert(candidate);
                        points.Add(candidate);
                    }
                }
                else
                {
                    grid.Insert(candidate);
                    points.Add(candidate);
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Generic uniform (grid-based) point generation with customizable bounds and containment checking.
    /// Computes grid spacing from volume/area and target count, iterates grid cells,
    /// checks containment, applies jitter, and re-checks containment.
    /// Used by shape strategies to implement Uniform distribution without duplicating the grid algorithm.
    /// </summary>
    /// <param name="boundsMin">Minimum corner of the bounding box for grid iteration.</param>
    /// <param name="boundsMax">Maximum corner of the bounding box for grid iteration.</param>
    /// <param name="isInBounds">Containment check for the target shape (called pre- and post-jitter).</param>
    /// <param name="targetCount">Approximate number of points to generate (grid spacing is derived from this).</param>
    /// <param name="jitter">Random offset fraction applied to each grid point (0 = none, 1 = full).</param>
    /// <param name="spacing">Reference spacing for jitter magnitude calculation.</param>
    /// <param name="rng">Random number generator for jitter.</param>
    /// <param name="flattenZ">When true, generates on XY plane (Z=0) using 2D area instead of 3D volume.</param>
    public static List<Vector3> GenerateUniformGeneric(
        Vector3 boundsMin, Vector3 boundsMax,
        Func<Vector3, bool> isInBounds,
        int targetCount, float jitter, float spacing, Random rng,
        bool flattenZ = false)
    {
        var points = new List<Vector3>();

        float xExtent = boundsMax.X - boundsMin.X;
        float yExtent = boundsMax.Y - boundsMin.Y;

        float gridSpacing;
        if (flattenZ)
        {
            float area = xExtent * yExtent;
            if (area <= 0f || targetCount <= 0) { return points; }
            float areaPerPoint = area / targetCount;
            gridSpacing = Mathf.Sqrt(areaPerPoint);
        }
        else
        {
            float zExtent = boundsMax.Z - boundsMin.Z;
            float volume = xExtent * yExtent * zExtent;
            if (volume <= 0f || targetCount <= 0) { return points; }
            float volumePerPoint = volume / targetCount;
            gridSpacing = Mathf.Pow(volumePerPoint, 1f / 3f);
        }

        for (float x = boundsMin.X; x <= boundsMax.X; x += gridSpacing)
        {
            for (float y = boundsMin.Y; y <= boundsMax.Y; y += gridSpacing)
            {
                if (flattenZ)
                {
                    var point = new Vector3(x, y, 0);
                    if (isInBounds(point))
                    {
                        point = ApplyJitter2D(point, jitter, spacing, rng);
                        if (isInBounds(point)) { points.Add(point); }
                    }
                }
                else
                {
                    for (float z = boundsMin.Z; z <= boundsMax.Z; z += gridSpacing)
                    {
                        var point = new Vector3(x, y, z);
                        if (isInBounds(point))
                        {
                            point = ApplyJitter(point, jitter, spacing, rng);
                            if (isInBounds(point)) { points.Add(point); }
                        }
                    }
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Generic Poisson disk sampling for Y-restricted slices of shapes with varying cross-sections.
    /// At each candidate Y, the shape's cross-section radii (Rx, Rz) are computed via the delegate,
    /// and a random point is generated within that elliptical cross-section.
    /// Used by Sphere (circular cross-section) and Ellipsoid (elliptical cross-section) slice generation.
    /// </summary>
    /// <param name="yMin">Minimum Y bound of the slice.</param>
    /// <param name="yMax">Maximum Y bound of the slice.</param>
    /// <param name="getCrossSection">Given a Y value, returns (crossRx, crossRz) for the shape's cross-section at that height.</param>
    /// <param name="isInBounds">Full containment check for the parent shape (called on jittered points).</param>
    /// <param name="minSpacing">Minimum distance between placed points.</param>
    /// <param name="targetCount">Desired number of points.</param>
    /// <param name="jitter">Jitter magnitude fraction.</param>
    /// <param name="rng">Random number generator.</param>
    public static List<Vector3> GeneratePoissonSliceGeneric(
        float yMin, float yMax,
        Func<float, (float crossRx, float crossRz)> getCrossSection,
        Func<Vector3, bool> isInBounds,
        float minSpacing,
        int targetCount,
        float jitter,
        Random rng)
    {
        var points = new List<Vector3>();
        var grid = new SpatialHashGrid3D(minSpacing);
        int maxAttempts = targetCount * 50;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            var (crossRx, crossRz) = getCrossSection(y);
            if (crossRx <= 0 || crossRz <= 0) { continue; }

            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble());
            float x = r * Mathf.Cos(angle) * crossRx;
            float z = r * Mathf.Sin(angle) * crossRz;

            var candidate = new Vector3(x, y, z);
            if (!grid.HasNeighborWithinDistance(candidate, minSpacing))
            {
                var jitteredPoint = ApplyJitter(candidate, jitter, minSpacing, rng);
                jitteredPoint.Y = Mathf.Clamp(jitteredPoint.Y, yMin, yMax);

                if (isInBounds(jitteredPoint))
                {
                    if (!grid.HasNeighborWithinDistance(jitteredPoint, minSpacing))
                    {
                        grid.Insert(jitteredPoint);
                        points.Add(jitteredPoint);
                    }
                    else
                    {
                        grid.Insert(candidate);
                        points.Add(candidate);
                    }
                }
                else
                {
                    grid.Insert(candidate);
                    points.Add(candidate);
                }
            }
        }

        return points;
    }

    /// <summary>
    /// Generates points within a 2D circle (flattened to XY plane), dispatching to the appropriate
    /// distribution algorithm. Shared by SphereCloudShape and CylinderCloudShape, which both
    /// project to circles when flattened.
    /// </summary>
    public static List<Vector3> GenerateFlatCircle(
        float radius, float yMin, float yMax, PointCloudGenerationParams p, Random rng)
    {
        Func<Vector3, bool> isInBounds = pt =>
            IsInsideCircle(pt, radius) && pt.Y >= yMin && pt.Y <= yMax;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCircle(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => GenerateUniformGeneric(
                new Vector3(-radius, yMin, 0),
                new Vector3(radius, yMax, 0),
                isInBounds,
                p.TargetCount, p.PositionJitter, p.MinSpacing, rng, flattenZ: true),
            _ => GeneratePoissonGeneric(
                r => GenerateRandomPointInCircle(radius, r),
                isInBounds,
                p.MinSpacing, p.TargetCount, p.PositionJitter, rng, flattenZ: true)
        };
    }

    /// <summary>
    /// Generates random points within a Y-bounded circle on the XY plane.
    /// Uses rejection sampling on the Y range after generating uniformly in the circle.
    /// Shared by SphereCloudShape and CylinderCloudShape.
    /// </summary>
    public static List<Vector3> GenerateRandomInCircle(
        float radius, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        int maxAttempts = count * 50;
        int attempts = 0;
        while (points.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var point = GenerateRandomPointInCircle(radius, rng);
            if (point.Y < yMin || point.Y > yMax) { continue; }
            point = ApplyJitter2D(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    #endregion
}
