namespace Jmodot.Core.PointCloud.Shapes;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates points distributed within an ellipsoid.
/// Supports PoissonDisk, Random, and Uniform distributions.
/// Scale components define the X, Y, Z radii of the ellipsoid.
/// When Y-cutoff is active, restricts the Y generation range accordingly.
/// </summary>
[GlobalClass]
public partial class EllipsoidCloudShape : PointCloudShapeStrategy
{
    public override List<Vector3> Generate(Vector3 scale, PointCloudGenerationParams p)
    {
        var rng = new Random(p.Seed);

        // Ellipsoid uses scale.Y for Y extent in both 2D (ellipse) and 3D modes
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * scale.Y : -scale.Y;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * scale.Y : scale.Y;

        if (p.FlattenToPlane)
        {
            return GenerateFlatEllipse(scale, yMin, yMax, p, rng);
        }

        if (p.HasYCutoff)
        {
            return p.Distribution switch
            {
                PointCloudDistribution.Random =>
                    GenerateRandomInYSlice(scale, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
                PointCloudDistribution.Uniform =>
                    GenerateUniformInEllipsoidSlice(scale, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
                _ => GeneratePoissonInYSlice(scale, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
            };
        }

        return p.Distribution switch
        {
            PointCloudDistribution.PoissonDisk => GeneratePoissonInEllipsoid(scale, p.MinSpacing, p.TargetCount, p.PositionJitter, rng),
            PointCloudDistribution.Random => GenerateRandomInEllipsoid(scale, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => GenerateUniformInEllipsoid(scale, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            _ => GeneratePoissonInEllipsoid(scale, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
        };
    }

    public override bool IsInside(Vector3 point, Vector3 scale)
    {
        return PointCloudGenerator.IsInsideEllipsoid(point, scale);
    }

    #region 2D Ellipse Generation (FlattenToPlane)

    private static List<Vector3> GenerateFlatEllipse(Vector3 radii, float yMin, float yMax, PointCloudGenerationParams p, Random rng)
    {
        Func<Vector3, bool> isInBounds = pt =>
            PointCloudGenerator.IsInsideEllipse(pt, radii.X, radii.Y) && pt.Y >= yMin && pt.Y <= yMax;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInEllipse(radii.X, radii.Y, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => PointCloudGenerator.GenerateUniformGeneric(
                new Vector3(-radii.X, yMin, 0),
                new Vector3(radii.X, yMax, 0),
                isInBounds,
                p.TargetCount, p.PositionJitter, p.MinSpacing, rng, flattenZ: true),
            _ => PointCloudGenerator.GeneratePoissonGeneric(
                r => PointCloudGenerator.GenerateRandomPointInEllipse(radii.X, radii.Y, r),
                isInBounds,
                p.MinSpacing, p.TargetCount, p.PositionJitter, rng, flattenZ: true)
        };
    }

    private static List<Vector3> GenerateRandomInEllipse(float rx, float ry, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        int maxAttempts = count * 50;
        int attempts = 0;
        while (points.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var point = PointCloudGenerator.GenerateRandomPointInEllipse(rx, ry, rng);
            if (point.Y < yMin || point.Y > yMax) { continue; }
            point = PointCloudGenerator.ApplyJitter2D(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    #endregion

    #region Distribution Algorithms

    private static List<Vector3> GenerateRandomInEllipsoid(Vector3 radii, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            var point = PointCloudGenerator.GenerateRandomPointInEllipsoid(radii, rng);
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GenerateUniformInEllipsoid(Vector3 radii, int targetCount, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>();

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
                    if (PointCloudGenerator.IsInsideEllipsoid(point, radii))
                    {
                        point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
                        if (PointCloudGenerator.IsInsideEllipsoid(point, radii))
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
        return PointCloudGenerator.GeneratePoissonGeneric(
            r => PointCloudGenerator.GenerateRandomPointInEllipsoid(radii, r),
            p => PointCloudGenerator.IsInsideEllipsoid(p, radii),
            minSpacing, targetCount, jitter, rng);
    }

    /// <summary>
    /// Generates random points within a Y-restricted slice of an ellipsoid.
    /// Uses cross-section-aware sampling: picks Y in [yMin, yMax], computes the elliptical
    /// cross-section radii at that Y, and generates a uniformly distributed point in that ellipse.
    /// No spacing constraints — returns exactly <paramref name="count"/> points.
    /// </summary>
    private static List<Vector3> GenerateRandomInYSlice(
        Vector3 radii, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);

            float yNorm = y / radii.Y;
            float crossFactor = 1.0f - yNorm * yNorm;
            if (crossFactor <= 0) { i--; continue; }

            float crossFactorSqrt = Mathf.Sqrt(crossFactor);
            float crossRx = radii.X * crossFactorSqrt;
            float crossRz = radii.Z * crossFactorSqrt;

            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble());
            var point = new Vector3(
                r * Mathf.Cos(angle) * crossRx,
                y,
                r * Mathf.Sin(angle) * crossRz);
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    /// <summary>
    /// Generates uniform (grid-based) points within a Y-restricted slice of an ellipsoid.
    /// Delegates to GenerateUniformGeneric with ellipsoid containment check and Y-bounded grid.
    /// </summary>
    private static List<Vector3> GenerateUniformInEllipsoidSlice(
        Vector3 radii, float yMin, float yMax, int targetCount, float jitter, float spacing, Random rng)
    {
        var boundsMin = new Vector3(-radii.X, yMin, -radii.Z);
        var boundsMax = new Vector3(radii.X, yMax, radii.Z);
        return PointCloudGenerator.GenerateUniformGeneric(
            boundsMin, boundsMax,
            p => PointCloudGenerator.IsInsideEllipsoid(p, radii) && p.Y >= yMin && p.Y <= yMax,
            targetCount, jitter, spacing, rng);
    }

    /// <summary>
    /// Generates points in a Y-restricted slice of the ellipsoid using Poisson disk sampling.
    /// Candidates are generated within the Y range and checked against ellipsoid containment.
    /// </summary>
    private static List<Vector3> GeneratePoissonInYSlice(
        Vector3 radii, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        var points = new List<Vector3>();
        int maxAttempts = targetCount * 50;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            // Generate candidate with Y restricted to [yMin, yMax]
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);

            // At this Y, the ellipsoid cross-section has reduced X/Z radii
            // Ellipsoid: (x/rx)² + (y/ry)² + (z/rz)² = 1
            // At given y: (x/rx)² + (z/rz)² = 1 - (y/ry)²
            float yNorm = y / radii.Y;
            float crossFactor = 1.0f - yNorm * yNorm;
            if (crossFactor <= 0) { continue; }

            float crossFactorSqrt = Mathf.Sqrt(crossFactor);
            float crossRx = radii.X * crossFactorSqrt;
            float crossRz = radii.Z * crossFactorSqrt;

            // Generate random point in the elliptical cross-section
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble());
            float x = r * Mathf.Cos(angle) * crossRx;
            float z = r * Mathf.Sin(angle) * crossRz;

            var candidate = new Vector3(x, y, z);
            float minDist = PointCloudGenerator.CalculateMinDistance(candidate, points);
            if (minDist >= minSpacing)
            {
                var jitteredPoint = PointCloudGenerator.ApplyJitter(candidate, jitter, minSpacing, rng);
                jitteredPoint.Y = Mathf.Clamp(jitteredPoint.Y, yMin, yMax);

                if (PointCloudGenerator.IsInsideEllipsoid(jitteredPoint, radii))
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
