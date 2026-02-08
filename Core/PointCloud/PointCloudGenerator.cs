namespace Jmodot.Core.PointCloud;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Static utility class for generating point distributions within 3D shapes.
/// All methods are pure functions with deterministic output given the same seed.
/// Designed for reuse across explosions, AoE effects, procedural placement, etc.
/// </summary>
public static class PointCloudGenerator
{
    private const int MaxRejectionIterations = 1000;

    #region Main API

    /// <summary>
    /// Generates a list of points distributed within a volume defined by the config.
    /// Entry point that dispatches to shape-specific generation methods.
    /// </summary>
    /// <param name="config">Configuration for distribution algorithm, shape, spacing, etc.</param>
    /// <param name="scale">Scale/radii of the shape (interpreted based on Shape type).</param>
    /// <param name="seed">Random seed for deterministic generation.</param>
    /// <returns>List of 3D positions within the specified volume.</returns>
    public static List<Vector3> GeneratePoints(PointCloudConfig config, Vector3 scale, int seed)
    {
        return config.Shape switch
        {
            PointCloudShape.Sphere => GenerateInSphere(scale.X, config, seed),
            PointCloudShape.Ellipsoid => GenerateInEllipsoid(scale, config, seed),
            PointCloudShape.Box => GenerateInBox(scale, config, seed),
            PointCloudShape.Cylinder => GenerateInCylinder(scale.X, scale.Y, config, seed),
            _ => GenerateInSphere(scale.X, config, seed)
        };
    }

    /// <summary>
    /// Generates points within a sphere using the specified distribution algorithm.
    /// </summary>
    public static List<Vector3> GenerateInSphere(float radius, PointCloudConfig config, int seed)
    {
        var rng = new Random(seed);

        return config.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInSphere(radius, config.TargetPointCount, config.PositionJitter, config.MinSpacing, rng),
            PointCloudDistribution.Uniform => GenerateUniformInSphere(radius, config.TargetPointCount, config.PositionJitter, config.MinSpacing, rng),
            PointCloudDistribution.PoissonDisk => GeneratePoissonInSphere(radius, config.MinSpacing, config.TargetPointCount, config.PositionJitter, rng),
            _ => GeneratePoissonInSphere(radius, config.MinSpacing, config.TargetPointCount, config.PositionJitter, rng)
        };
    }

    /// <summary>
    /// Generates points within an ellipsoid using the specified distribution algorithm.
    /// </summary>
    public static List<Vector3> GenerateInEllipsoid(Vector3 radii, PointCloudConfig config, int seed)
    {
        var rng = new Random(seed);

        return config.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInEllipsoid(radii, config.TargetPointCount, config.PositionJitter, config.MinSpacing, rng),
            PointCloudDistribution.Uniform => GenerateUniformInEllipsoid(radii, config.TargetPointCount, config.PositionJitter, config.MinSpacing, rng),
            PointCloudDistribution.PoissonDisk => GeneratePoissonInEllipsoid(radii, config.MinSpacing, config.TargetPointCount, config.PositionJitter, rng),
            _ => GeneratePoissonInEllipsoid(radii, config.MinSpacing, config.TargetPointCount, config.PositionJitter, rng)
        };
    }

    /// <summary>
    /// Generates points within a vertical slice of a sphere.
    /// Used for tier-based generation (bottom, mid, top layers).
    /// </summary>
    public static List<Vector3> GenerateInSlice(float radius, PointCloudConfig config, PointCloudSlice slice, int seed)
    {
        var rng = new Random(seed);

        // Map normalized Y range to actual Y coordinates
        // slice.YMin=0 maps to -radius, slice.YMax=1 maps to +radius
        float yMin = (slice.YMin * 2 - 1) * radius;
        float yMax = (slice.YMax * 2 - 1) * radius;

        // Adjust target count based on slice height and density multiplier
        int adjustedTarget = (int)(config.TargetPointCount * slice.Height * slice.DensityMultiplier);
        adjustedTarget = Math.Max(1, adjustedTarget);

        return GeneratePoissonInSlice(radius, yMin, yMax, config.MinSpacing, adjustedTarget, config.PositionJitter, rng);
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
        // Rejection sampling: generate in cube, reject if outside sphere
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

        // Fallback: return center point if rejection sampling fails (should never happen with valid RNG)
        JmoLogger.Warning(typeof(PointCloudGenerator),
            $"Rejection sampling failed after {MaxRejectionIterations} iterations, returning origin");
        return Vector3.Zero;
    }

    /// <summary>
    /// Generates a random point uniformly distributed inside an ellipsoid.
    /// </summary>
    public static Vector3 GenerateRandomPointInEllipsoid(Vector3 radii, Random rng)
    {
        // Generate in unit sphere, then scale to ellipsoid
        var unitPoint = GenerateRandomPointInSphere(1.0f, rng);
        return new Vector3(unitPoint.X * radii.X, unitPoint.Y * radii.Y, unitPoint.Z * radii.Z);
    }

    /// <summary>
    /// Applies random jitter to a point position.
    /// </summary>
    /// <param name="point">Original point position.</param>
    /// <param name="jitterAmount">Jitter strength (0 = none, 1 = full).</param>
    /// <param name="spacing">Reference spacing for jitter magnitude.</param>
    /// <param name="rng">Random number generator.</param>
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

    #region Distribution Algorithms - Sphere

    private static List<Vector3> GenerateRandomInSphere(float radius, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            var point = GenerateRandomPointInSphere(radius, rng);
            point = ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GenerateUniformInSphere(float radius, int targetCount, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>();

        // Approximate grid spacing to hit target count
        // Volume of sphere = (4/3)πr³, divide by target count
        float volume = (4f / 3f) * Mathf.Pi * radius * radius * radius;
        float volumePerPoint = volume / targetCount;
        float gridSpacing = Mathf.Pow(volumePerPoint, 1f / 3f);

        // Generate grid points and keep those inside sphere
        for (float x = -radius; x <= radius; x += gridSpacing)
        {
            for (float y = -radius; y <= radius; y += gridSpacing)
            {
                for (float z = -radius; z <= radius; z += gridSpacing)
                {
                    var point = new Vector3(x, y, z);
                    if (IsInsideSphere(point, radius))
                    {
                        point = ApplyJitter(point, jitter, spacing, rng);
                        if (IsInsideSphere(point, radius)) // Re-check after jitter
                        {
                            points.Add(point);
                        }
                    }
                }
            }
        }

        return points;
    }

    private static List<Vector3> GeneratePoissonInSphere(float radius, float minSpacing, int targetCount, float jitter, Random rng)
    {
        return GeneratePoissonGeneric(
            r => GenerateRandomPointInSphere(radius, r),
            p => IsInsideSphere(p, radius),
            minSpacing, targetCount, jitter, rng);
    }

    #endregion

    #region Distribution Algorithms - Ellipsoid

    private static List<Vector3> GenerateRandomInEllipsoid(Vector3 radii, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            var point = GenerateRandomPointInEllipsoid(radii, rng);
            point = ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GenerateUniformInEllipsoid(Vector3 radii, int targetCount, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>();

        // Approximate grid spacing
        float volume = (4f / 3f) * Mathf.Pi * radii.X * radii.Y * radii.Z;
        float volumePerPoint = volume / targetCount;
        float gridSpacing = Mathf.Pow(volumePerPoint, 1f / 3f);

        for (float x = -radii.X; x <= radii.X; x += gridSpacing)
        {
            for (float y = -radii.Y; y <= radii.Y; y += gridSpacing)
            {
                for (float z = -radii.Z; z <= radii.Z; z += gridSpacing)
                {
                    var point = new Vector3(x, y, z);
                    if (IsInsideEllipsoid(point, radii))
                    {
                        point = ApplyJitter(point, jitter, spacing, rng);
                        if (IsInsideEllipsoid(point, radii))
                        {
                            points.Add(point);
                        }
                    }
                }
            }
        }

        return points;
    }

    private static List<Vector3> GeneratePoissonInEllipsoid(Vector3 radii, float minSpacing, int targetCount, float jitter, Random rng)
    {
        return GeneratePoissonGeneric(
            r => GenerateRandomPointInEllipsoid(radii, r),
            p => IsInsideEllipsoid(p, radii),
            minSpacing, targetCount, jitter, rng);
    }

    #endregion

    #region Generic Poisson Algorithm

    /// <summary>
    /// Generic Poisson disk sampling with customizable candidate generation and bounds checking.
    /// Reduces duplication across sphere, ellipsoid, and other shapes.
    /// </summary>
    /// <param name="generateCandidate">Function to generate a random candidate point.</param>
    /// <param name="isInBounds">Function to check if a point is within valid bounds.</param>
    /// <param name="minSpacing">Minimum distance between points.</param>
    /// <param name="targetCount">Target number of points to generate.</param>
    /// <param name="jitter">Jitter amount (0-1).</param>
    /// <param name="rng">Random number generator.</param>
    private static List<Vector3> GeneratePoissonGeneric(
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

    #region Distribution Algorithms - Slice

    private static List<Vector3> GeneratePoissonInSlice(float radius, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        var points = new List<Vector3>();
        int maxAttempts = targetCount * 50; // More attempts for constrained slice
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            // Generate random Y within slice bounds
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);

            // At this Y, the sphere cross-section has radius sqrt(R² - y²)
            float crossSectionRadius = Mathf.Sqrt(radius * radius - y * y);
            if (crossSectionRadius <= 0) { continue; }

            // Generate random point in the circular cross-section
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * crossSectionRadius; // sqrt for uniform disk sampling
            float x = r * Mathf.Cos(angle);
            float z = r * Mathf.Sin(angle);

            var candidate = new Vector3(x, y, z);

            float minDist = CalculateMinDistance(candidate, points);
            if (minDist >= minSpacing)
            {
                var jitteredPoint = ApplyJitter(candidate, jitter, minSpacing, rng);

                // Clamp Y to stay in slice
                jitteredPoint.Y = Mathf.Clamp(jitteredPoint.Y, yMin, yMax);

                if (IsInsideSphere(jitteredPoint, radius))
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

    #region Distribution Algorithms - Box & Cylinder (Basic implementations)

    private static List<Vector3> GenerateInBox(Vector3 halfExtents, PointCloudConfig config, int seed)
    {
        var rng = new Random(seed);
        var points = new List<Vector3>();

        if (config.Distribution == PointCloudDistribution.Random)
        {
            for (int i = 0; i < config.TargetPointCount; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * halfExtents.X;
                float y = (float)(rng.NextDouble() * 2 - 1) * halfExtents.Y;
                float z = (float)(rng.NextDouble() * 2 - 1) * halfExtents.Z;
                points.Add(new Vector3(x, y, z));
            }
        }
        else
        {
            // Poisson in box
            int maxAttempts = config.TargetPointCount * 30;
            int attempts = 0;

            while (points.Count < config.TargetPointCount && attempts < maxAttempts)
            {
                attempts++;
                float x = (float)(rng.NextDouble() * 2 - 1) * halfExtents.X;
                float y = (float)(rng.NextDouble() * 2 - 1) * halfExtents.Y;
                float z = (float)(rng.NextDouble() * 2 - 1) * halfExtents.Z;
                var candidate = new Vector3(x, y, z);

                if (CalculateMinDistance(candidate, points) >= config.MinSpacing)
                {
                    var jittered = ApplyJitter(candidate, config.PositionJitter, config.MinSpacing, rng);

                    // Re-check bounds after jitter
                    if (Math.Abs(jittered.X) <= halfExtents.X &&
                        Math.Abs(jittered.Y) <= halfExtents.Y &&
                        Math.Abs(jittered.Z) <= halfExtents.Z)
                    {
                        points.Add(jittered);
                    }
                    else
                    {
                        // Fallback to un-jittered candidate
                        points.Add(candidate);
                    }
                }
            }
        }

        return points;
    }

    private static List<Vector3> GenerateInCylinder(float radius, float halfHeight, PointCloudConfig config, int seed)
    {
        var rng = new Random(seed);
        var points = new List<Vector3>();

        if (config.Distribution == PointCloudDistribution.Random)
        {
            for (int i = 0; i < config.TargetPointCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.Tau);
                float r = (float)Math.Sqrt(rng.NextDouble()) * radius;
                float y = (float)(rng.NextDouble() * 2 - 1) * halfHeight;
                points.Add(new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle)));
            }
        }
        else
        {
            int maxAttempts = config.TargetPointCount * 30;
            int attempts = 0;

            while (points.Count < config.TargetPointCount && attempts < maxAttempts)
            {
                attempts++;
                float angle = (float)(rng.NextDouble() * Mathf.Tau);
                float r = (float)Math.Sqrt(rng.NextDouble()) * radius;
                float y = (float)(rng.NextDouble() * 2 - 1) * halfHeight;
                var candidate = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));

                if (CalculateMinDistance(candidate, points) >= config.MinSpacing)
                {
                    var jittered = ApplyJitter(candidate, config.PositionJitter, config.MinSpacing, rng);

                    // Re-check bounds after jitter
                    float jitteredR = Mathf.Sqrt(jittered.X * jittered.X + jittered.Z * jittered.Z);
                    if (jitteredR <= radius && Math.Abs(jittered.Y) <= halfHeight)
                    {
                        points.Add(jittered);
                    }
                    else
                    {
                        // Fallback to un-jittered candidate
                        points.Add(candidate);
                    }
                }
            }
        }

        return points;
    }

    #endregion
}
