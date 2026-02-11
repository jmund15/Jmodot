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
    /// </summary>
    /// <param name="config">Configuration for distribution algorithm, shape, spacing, etc.</param>
    /// <param name="scale">Scale/radii of the shape (interpreted by the shape strategy).</param>
    /// <param name="seed">Random seed for deterministic generation.</param>
    /// <param name="targetCountOverride">Optional override for config.TargetPointCount (e.g., density-scaled count).</param>
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

        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = targetCountOverride ?? config.TargetPointCount,
            YMinNormalized = yMinOverride ?? config.YMinNormalized,
            YMaxNormalized = 1.0f,
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
        int adjustedTarget = (int)(config.TargetPointCount * slice.Height * slice.DensityMultiplier);
        adjustedTarget = Math.Max(1, adjustedTarget);

        var shape = new SphereCloudShape();
        var genParams = new PointCloudGenerationParams
        {
            Distribution = config.Distribution,
            MinSpacing = config.MinSpacing,
            PositionJitter = config.PositionJitter,
            TargetCount = adjustedTarget,
            YMinNormalized = slice.YMin,
            YMaxNormalized = slice.YMax,
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
    public static List<Vector3> GeneratePoissonGeneric(
        Func<Random, Vector3> generateCandidate,
        Func<Vector3, bool> isInBounds,
        float minSpacing,
        int targetCount,
        float jitter,
        Random rng)
    {
        var points = new List<Vector3>();
        int maxAttempts = targetCount * 30;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            var candidate = generateCandidate(rng);

            float minDist = CalculateMinDistance(candidate, points);
            if (minDist >= minSpacing)
            {
                var jitteredPoint = ApplyJitter(candidate, jitter, minSpacing, rng);
                if (isInBounds(jitteredPoint))
                {
                    float jitteredMinDist = CalculateMinDistance(jitteredPoint, points);
                    if (jitteredMinDist >= minSpacing)
                    {
                        points.Add(jitteredPoint);
                    }
                    else
                    {
                        points.Add(candidate);
                    }
                }
                else
                {
                    points.Add(candidate);
                }
            }
        }

        return points;
    }

    #endregion
}
