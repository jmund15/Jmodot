namespace Jmodot.Core.AI.Perception;

using System.Collections.Generic;

/// <summary>
/// Pure static methods for fusing multiple sensor contributions into a single value.
/// Lifecycle-agnostic — operates on CurrentConfidence from each contribution
/// regardless of whether the sensor is active or decaying.
/// </summary>
internal static class PerceptionFusion
{
    public static float FuseConfidence(IEnumerable<SensorContribution> contributions, FusionMode mode)
    {
        var result = 0f;

        foreach (var contrib in contributions)
        {
            var confidence = contrib.CurrentConfidence;
            switch (mode)
            {
                case FusionMode.Max:
                    if (confidence > result) { result = confidence; }
                    break;
                case FusionMode.Additive:
                    result += confidence;
                    break;
            }
        }

        return Mathf.Clamp(result, 0f, 1f);
    }

    public static SensorContribution? SelectBestContribution(IEnumerable<SensorContribution> contributions)
    {
        SensorContribution? best = null;
        var bestConfidence = -1f;

        foreach (var contrib in contributions)
        {
            var confidence = contrib.CurrentConfidence;
            if (confidence > bestConfidence ||
                (confidence == bestConfidence && best != null && contrib.LastUpdateTime > best.LastUpdateTime))
            {
                best = contrib;
                bestConfidence = confidence;
            }
        }

        return best;
    }
}
