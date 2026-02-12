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

        if (p.FlattenToPlane)
        {
            return GenerateFlatCircle(radius, p, rng);
        }

        // If Y bounds restrict the full shape, use slice-aware generation
        if (p.HasYCutoff)
        {
            float yMin = (p.YMinNormalized * 2 - 1) * radius;
            float yMax = (p.YMaxNormalized * 2 - 1) * radius;
            return GeneratePoissonInSlice(radius, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng);
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

    private static List<Vector3> GenerateFlatCircle(float radius, PointCloudGenerationParams p, Random rng)
    {
        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCircle(radius, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            _ => PointCloudGenerator.GeneratePoissonGeneric(
                r => PointCloudGenerator.GenerateRandomPointInCircle(radius, r),
                pt => PointCloudGenerator.IsInsideCircle(pt, radius),
                p.MinSpacing, p.TargetCount, p.PositionJitter, rng, flattenZ: true)
        };
    }

    private static List<Vector3> GenerateRandomInCircle(float radius, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            var point = PointCloudGenerator.GenerateRandomPointInCircle(radius, rng);
            point = PointCloudGenerator.ApplyJitter2D(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    #endregion

    #region Slice Generation

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
