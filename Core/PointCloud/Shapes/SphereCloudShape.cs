namespace Jmodot.Core.PointCloud.Shapes;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates points distributed within a sphere.
/// Supports PoissonDisk, Random, and Uniform distributions.
/// When YMinNormalized > 0, generates in a vertical slice (hemisphere, quarter, etc.).
/// Scale.X is used as the sphere radius.
/// </summary>
[GlobalClass]
public partial class SphereCloudShape : PointCloudShapeStrategy
{
    public override List<Vector3> Generate(Vector3 scale, PointCloudGenerationParams p)
    {
        float radius = scale.X;
        var rng = new Random(p.Seed);

        // Sphere uses radius for Y extent in both 2D (circle) and 3D modes
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * radius : -radius;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * radius : radius;

        if (p.FlattenToPlane)
        {
            return GenerateFlatCircle(radius, yMin, yMax, p, rng);
        }

        if (p.HasYCutoff)
        {
            return p.Distribution switch
            {
                PointCloudDistribution.Random =>
                    GenerateRandomInSlice(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
                PointCloudDistribution.Uniform =>
                    GenerateUniformInSphereSlice(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
                _ => GeneratePoissonInSlice(radius, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
            };
        }

        return p.Distribution switch
        {
            PointCloudDistribution.PoissonDisk => GeneratePoissonInSphere(radius, p.MinSpacing, p.TargetCount, p.PositionJitter, rng),
            PointCloudDistribution.Random => GenerateRandomInSphere(radius, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => GenerateUniformInSphere(radius, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            _ => GeneratePoissonInSphere(radius, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
        };
    }

    public override bool IsInside(Vector3 point, Vector3 scale)
    {
        return PointCloudGenerator.IsInsideSphere(point, scale.X);
    }

    #region Distribution Algorithms

    private static List<Vector3> GenerateRandomInSphere(float radius, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            var point = PointCloudGenerator.GenerateRandomPointInSphere(radius, rng);
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GenerateUniformInSphere(float radius, int targetCount, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>();

        float volume = (4f / 3f) * Mathf.Pi * radius * radius * radius;
        float volumePerPoint = volume / targetCount;
        float gridSpacing = Mathf.Pow(volumePerPoint, 1f / 3f);

        for (float x = -radius; x <= radius; x += gridSpacing)
        {
            for (float y = -radius; y <= radius; y += gridSpacing)
            {
                for (float z = -radius; z <= radius; z += gridSpacing)
                {
                    var point = new Vector3(x, y, z);
                    if (PointCloudGenerator.IsInsideSphere(point, radius))
                    {
                        point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
                        if (PointCloudGenerator.IsInsideSphere(point, radius))
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
        return PointCloudGenerator.GeneratePoissonGeneric(
            r => PointCloudGenerator.GenerateRandomPointInSphere(radius, r),
            p => PointCloudGenerator.IsInsideSphere(p, radius),
            minSpacing, targetCount, jitter, rng);
    }

    #endregion

    #region 2D Circle Generation (FlattenToPlane)

    private static List<Vector3> GenerateFlatCircle(float radius, float yMin, float yMax, PointCloudGenerationParams p, Random rng)
    {
        Func<Vector3, bool> isInBounds = pt =>
            PointCloudGenerator.IsInsideCircle(pt, radius) && pt.Y >= yMin && pt.Y <= yMax;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCircle(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => PointCloudGenerator.GenerateUniformGeneric(
                new Vector3(-radius, yMin, 0),
                new Vector3(radius, yMax, 0),
                isInBounds,
                p.TargetCount, p.PositionJitter, p.MinSpacing, rng, flattenZ: true),
            _ => PointCloudGenerator.GeneratePoissonGeneric(
                r => PointCloudGenerator.GenerateRandomPointInCircle(radius, r),
                isInBounds,
                p.MinSpacing, p.TargetCount, p.PositionJitter, rng, flattenZ: true)
        };
    }

    private static List<Vector3> GenerateRandomInCircle(float radius, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        int maxAttempts = count * 50;
        int attempts = 0;
        while (points.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var point = PointCloudGenerator.GenerateRandomPointInCircle(radius, rng);
            if (point.Y < yMin || point.Y > yMax) { continue; }
            point = PointCloudGenerator.ApplyJitter2D(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    #endregion

    #region Slice Generation

    /// <summary>
    /// Generates random points within a Y-restricted slice of a sphere.
    /// Uses cross-section-aware sampling: picks Y in [yMin, yMax], computes the circular
    /// cross-section radius at that Y, and generates a uniformly distributed point in that disk.
    /// No spacing constraints — returns exactly <paramref name="count"/> points.
    /// </summary>
    private static List<Vector3> GenerateRandomInSlice(
        float radius, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            float crossR = Mathf.Sqrt(Mathf.Max(0, radius * radius - y * y));
            if (crossR <= 0) { i--; continue; }

            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * crossR;
            var point = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    /// <summary>
    /// Generates uniform (grid-based) points within a Y-restricted slice of a sphere.
    /// Delegates to GenerateUniformGeneric with sphere containment check and Y-bounded grid.
    /// </summary>
    private static List<Vector3> GenerateUniformInSphereSlice(
        float radius, float yMin, float yMax, int targetCount, float jitter, float spacing, Random rng)
    {
        var boundsMin = new Vector3(-radius, yMin, -radius);
        var boundsMax = new Vector3(radius, yMax, radius);
        return PointCloudGenerator.GenerateUniformGeneric(
            boundsMin, boundsMax,
            p => PointCloudGenerator.IsInsideSphere(p, radius) && p.Y >= yMin && p.Y <= yMax,
            targetCount, jitter, spacing, rng);
    }

    private static List<Vector3> GeneratePoissonInSlice(float radius, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        var points = new List<Vector3>();
        int maxAttempts = targetCount * 50;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            float crossSectionRadius = Mathf.Sqrt(radius * radius - y * y);
            if (crossSectionRadius <= 0) { continue; }

            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * crossSectionRadius;
            float x = r * Mathf.Cos(angle);
            float z = r * Mathf.Sin(angle);

            var candidate = new Vector3(x, y, z);
            float minDist = PointCloudGenerator.CalculateMinDistance(candidate, points);
            if (minDist >= minSpacing)
            {
                var jitteredPoint = PointCloudGenerator.ApplyJitter(candidate, jitter, minSpacing, rng);
                jitteredPoint.Y = Mathf.Clamp(jitteredPoint.Y, yMin, yMax);

                if (PointCloudGenerator.IsInsideSphere(jitteredPoint, radius))
                {
                    float jitteredMinDist = PointCloudGenerator.CalculateMinDistance(jitteredPoint, points);
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
