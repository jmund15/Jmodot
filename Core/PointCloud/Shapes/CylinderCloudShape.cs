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
            return PointCloudGenerator.GenerateFlatCircle(radius, flatYMin, flatYMax, p, rng);
        }

        // 3D: Y extent is halfHeight
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * halfHeight : -halfHeight;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * halfHeight : halfHeight;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCylinder(radius, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
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

    private static List<Vector3> GenerateRandomInCylinder(float radius, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.Tau);
            float r = (float)Math.Sqrt(rng.NextDouble()) * radius;
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            var point = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GeneratePoissonInCylinder(float radius, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        return PointCloudGenerator.GeneratePoissonGeneric(
            r =>
            {
                float angle = (float)(r.NextDouble() * Mathf.Tau);
                float rad = (float)Math.Sqrt(r.NextDouble()) * radius;
                float y = (float)(r.NextDouble() * (yMax - yMin) + yMin);
                return new Vector3(rad * Mathf.Cos(angle), y, rad * Mathf.Sin(angle));
            },
            p => p.X * p.X + p.Z * p.Z <= radius * radius && p.Y >= yMin && p.Y <= yMax,
            minSpacing, targetCount, jitter, rng);
    }

    #endregion
}
