namespace Jmodot.Core.PointCloud.Shapes;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Generates points distributed within an axis-aligned box.
/// Supports PoissonDisk and Random distributions.
/// Scale components define the half-extents in each axis.
/// When Y-cutoff is active, restricts the Y generation range accordingly.
/// </summary>
[GlobalClass]
public partial class BoxCloudShape : PointCloudShapeStrategy
{
    public override List<Vector3> Generate(Vector3 scale, PointCloudGenerationParams p)
    {
        var rng = new Random(p.Seed);

        // Box uses scale.Y for Y extent in both 2D (rectangle) and 3D modes
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * scale.Y : -scale.Y;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * scale.Y : scale.Y;

        if (p.FlattenToPlane)
        {
            return GenerateFlatRectangle(scale, yMin, yMax, p, rng);
        }

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInBox(scale, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform =>
                GenerateUniformInBox(scale, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            _ => GeneratePoissonInBox(scale, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
        };
    }

    public override bool IsInside(Vector3 point, Vector3 scale)
    {
        return Math.Abs(point.X) <= scale.X &&
               Math.Abs(point.Y) <= scale.Y &&
               Math.Abs(point.Z) <= scale.Z;
    }

    #region 2D Rectangle Generation (FlattenToPlane)

    private static List<Vector3> GenerateFlatRectangle(Vector3 halfExtents, float yMin, float yMax, PointCloudGenerationParams p, Random rng)
    {
        Func<Vector3, bool> isInBounds = pt =>
            Math.Abs(pt.X) <= halfExtents.X && pt.Y >= yMin && pt.Y <= yMax;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInRectangle(halfExtents, yMin, yMax, p.TargetCount, p.PositionJitter, p.MinSpacing, rng),
            PointCloudDistribution.Uniform => PointCloudGenerator.GenerateUniformGeneric(
                new Vector3(-halfExtents.X, yMin, 0),
                new Vector3(halfExtents.X, yMax, 0),
                isInBounds,
                p.TargetCount, p.PositionJitter, p.MinSpacing, rng, flattenZ: true),
            _ => PointCloudGenerator.GeneratePoissonGeneric(
                r =>
                {
                    float x = (float)(r.NextDouble() * 2 - 1) * halfExtents.X;
                    float y = (float)(r.NextDouble() * (yMax - yMin) + yMin);
                    return new Vector3(x, y, 0);
                },
                isInBounds,
                p.MinSpacing, p.TargetCount, p.PositionJitter, rng, flattenZ: true)
        };
    }

    private static List<Vector3> GenerateRandomInRectangle(Vector3 halfExtents, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * halfExtents.X;
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            var point = new Vector3(x, y, 0);
            point = PointCloudGenerator.ApplyJitter2D(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    #endregion

    #region Distribution Algorithms

    private static List<Vector3> GenerateUniformInBox(
        Vector3 halfExtents, float yMin, float yMax, int targetCount, float jitter, float spacing, Random rng)
    {
        var boundsMin = new Vector3(-halfExtents.X, yMin, -halfExtents.Z);
        var boundsMax = new Vector3(halfExtents.X, yMax, halfExtents.Z);
        return PointCloudGenerator.GenerateUniformGeneric(
            boundsMin, boundsMax,
            p => Math.Abs(p.X) <= halfExtents.X && p.Y >= yMin && p.Y <= yMax && Math.Abs(p.Z) <= halfExtents.Z,
            targetCount, jitter, spacing, rng);
    }

    private static List<Vector3> GenerateRandomInBox(Vector3 halfExtents, float yMin, float yMax, int count, float jitter, float spacing, Random rng)
    {
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * halfExtents.X;
            float y = (float)(rng.NextDouble() * (yMax - yMin) + yMin);
            float z = (float)(rng.NextDouble() * 2 - 1) * halfExtents.Z;
            var point = new Vector3(x, y, z);
            point = PointCloudGenerator.ApplyJitter(point, jitter, spacing, rng);
            points.Add(point);
        }
        return points;
    }

    private static List<Vector3> GeneratePoissonInBox(Vector3 halfExtents, float yMin, float yMax, float minSpacing, int targetCount, float jitter, Random rng)
    {
        return PointCloudGenerator.GeneratePoissonGeneric(
            r =>
            {
                float x = (float)(r.NextDouble() * 2 - 1) * halfExtents.X;
                float y = (float)(r.NextDouble() * (yMax - yMin) + yMin);
                float z = (float)(r.NextDouble() * 2 - 1) * halfExtents.Z;
                return new Vector3(x, y, z);
            },
            p => Math.Abs(p.X) <= halfExtents.X && p.Y >= yMin && p.Y <= yMax && Math.Abs(p.Z) <= halfExtents.Z,
            minSpacing, targetCount, jitter, rng);
    }

    #endregion
}
