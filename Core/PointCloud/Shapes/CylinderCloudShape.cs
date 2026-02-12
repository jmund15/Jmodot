namespace Jmodot.Core.PointCloud.Shapes;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates points distributed within a vertical cylinder.
/// Supports PoissonDisk and Random distributions.
/// Scale.X = radius, Scale.Y = half-height. Scale.Z is unused.
/// When Y-cutoff is active, restricts the Y generation range accordingly.
/// </summary>
[GlobalClass]
public partial class CylinderCloudShape : PointCloudShapeStrategy
{
    public override List<Vector3> Generate(Vector3 scale, PointCloudGenerationParams p)
    {
        float radius = scale.X;
        float halfHeight = scale.Y;
        var rng = new Random(p.Seed);

        // Cylinder flattened to XY is a circle — Y extent is radius, not halfHeight
        if (p.FlattenToPlane)
        {
            float flatYMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * radius : -radius;
            float flatYMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * radius : radius;
            return GenerateFlatCircle(radius, flatYMin, flatYMax, p, rng);
        }

        // 3D: Y extent is halfHeight
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * halfHeight : -halfHeight;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * halfHeight : halfHeight;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCylinder(radius, yMin, yMax, p.TargetCount, rng),
            PointCloudDistribution.Uniform =>
                GenerateUniformInCylinder(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            _ => GeneratePoissonInCylinder(radius, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
        };
    }

    public override bool IsInside(Vector3 point, Vector3 scale)
    {
        float horizontalDistSq = point.X * point.X + point.Z * point.Z;
        return horizontalDistSq <= scale.X * scale.X && Math.Abs(point.Y) <= scale.Y;
    }

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

    #region Distribution Algorithms

    private static List<Vector3> GenerateUniformInCylinder(
        float radius, float yMin, float yMax, int targetCount, float jitter, float spacing, Random rng)
    {
        var boundsMin = new Vector3(-radius, yMin, -radius);
        var boundsMax = new Vector3(radius, yMax, radius);
        return PointCloudGenerator.GenerateUniformGeneric(
            boundsMin, boundsMax,
            p => p.X * p.X + p.Z * p.Z <= radius * radius && p.Y >= yMin && p.Y <= yMax,
            targetCount, jitter, spacing, rng);
    }

    private static List<Vector3> GenerateRandomInCylinder(float radius, float yMin, float yMax, int count, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * radius;
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            points.Add(new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle)));
        }
        return points;
    }

    private static List<Vector3> GeneratePoissonInCylinder(float radius, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        var points = new List<Vector3>();
        int maxAttempts = targetCount * 30;
        int attempts = 0;

        while (points.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * radius;
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            var candidate = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));

            if (PointCloudGenerator.CalculateMinDistance(candidate, points) >= minSpacing)
            {
                var jittered = PointCloudGenerator.ApplyJitter(candidate, jitter, minSpacing, rng);

                // Clamp Y to generation range after jitter
                jittered.Y = Mathf.Clamp(jittered.Y, yMin, yMax);

                float jitteredR = Mathf.Sqrt(jittered.X * jittered.X + jittered.Z * jittered.Z);
                if (jitteredR <= radius)
                {
                    points.Add(jittered);
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
