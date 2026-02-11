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

        // Compute actual Y bounds from normalized values
        float yMin = p.HasYCutoff ? (p.YMinNormalized * 2 - 1) * halfHeight : -halfHeight;
        float yMax = p.HasYCutoff ? (p.YMaxNormalized * 2 - 1) * halfHeight : halfHeight;

        return p.Distribution switch
        {
            PointCloudDistribution.Random => GenerateRandomInCylinder(radius, yMin, yMax, p.TargetCount, rng),
            _ => GeneratePoissonInCylinder(radius, yMin, yMax, p.MinSpacing, p.TargetCount, p.PositionJitter, rng)
        };
    }

    public override bool IsInside(Vector3 point, Vector3 scale)
    {
        float horizontalDistSq = point.X * point.X + point.Z * point.Z;
        return horizontalDistSq <= scale.X * scale.X && Math.Abs(point.Y) <= scale.Y;
    }

    #region Distribution Algorithms

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
