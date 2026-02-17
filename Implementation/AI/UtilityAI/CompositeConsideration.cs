// --- CompositeConsideration.cs ---
namespace JmoAI.UtilityAI;

using System.Linq;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Shared;

public enum ConsiderationOperator
{
    Add,            // Suggestion: 2 considerations only
    Subtract,       // Suggestion: 2 considerations only
    Multiply,       // Suggestion: 2 considerations only
    Divide,         // Suggestion: 2 considerations only
    Average,
    Max,
    Min,
    Random,         // Gets random consideration from list
    WeightedAverage,// Weighted average using Weights array
    Veto,           // Any zero vetoes the result (else multiply)
    ThresholdGate   // Any below threshold vetoes (else average)
}

/// <summary>
/// Combines multiple considerations using the specified operator.
/// Useful for complex utility calculations like "flee if (low health AND high threat)".
/// </summary>
[GlobalClass, Tool]
public partial class CompositeConsideration : UtilityConsideration
{
    [Export]
    protected Godot.Collections.Array<UtilityConsideration> Considerations = new();

    [Export]
    protected ConsiderationOperator Operator = ConsiderationOperator.Average;

    /// <summary>
    /// Weights for WeightedAverage mode. If fewer weights than children,
    /// remaining children use weight 1.0.
    /// </summary>
    [Export]
    protected Godot.Collections.Array<float> Weights = new();

    /// <summary>
    /// Threshold for ThresholdGate mode. Any child below this threshold
    /// causes the entire composite to return 0.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    protected float Threshold = 0.1f;

    protected override float CalculateBaseScore(IBlackboard context)
    {
        if (Considerations.Count == 0) { return 0f; }

        if (Operator == ConsiderationOperator.Random)
        {
            var randConsid = Considerations[JmoRng.Rnd.Next(0, Considerations.Count)];
            return randConsid.Evaluate(context);
        }

        // Evaluate all children first for modes that need the full list
        var scores = Considerations.Select(c => c.Evaluate(context)).ToList();

        // Handle special composite modes
        switch (Operator)
        {
            case ConsiderationOperator.WeightedAverage:
                return CalculateWeightedAverage(scores);

            case ConsiderationOperator.Veto:
                // Any zero vetoes the whole thing
                if (scores.Any(s => s <= 0f))
                {
                    return 0f;
                }
                // Otherwise multiply all
                return Mathf.Clamp(scores.Aggregate(1f, (acc, s) => acc * s), 0f, 1f);

            case ConsiderationOperator.ThresholdGate:
                // Any below threshold vetoes
                if (scores.Any(s => s < Threshold))
                {
                    return 0f;
                }
                // Otherwise average
                return Mathf.Clamp(scores.Average(), 0f, 1f);
        }

        // Legacy iterative approach for the original operators
        float compositeResult = scores[0];

        foreach (var result in scores.Skip(1))
        {
            switch (Operator)
            {
                case ConsiderationOperator.Add:
                    compositeResult += result;
                    break;
                case ConsiderationOperator.Subtract:
                    compositeResult -= result;
                    break;
                case ConsiderationOperator.Multiply:
                    compositeResult *= result;
                    break;
                case ConsiderationOperator.Divide:
                    // Guard against division by zero
                    if (result > 0.001f)
                    {
                        compositeResult /= result;
                    }
                    break;
                case ConsiderationOperator.Average:
                    compositeResult += result;
                    break;
                case ConsiderationOperator.Max:
                    compositeResult = Mathf.Max(compositeResult, result);
                    break;
                case ConsiderationOperator.Min:
                    compositeResult = Mathf.Min(compositeResult, result);
                    break;
                default:
                    return 0f;
            }
        }

        if (Operator == ConsiderationOperator.Average)
        {
            compositeResult /= Considerations.Count;
        }

        return Mathf.Clamp(compositeResult, 0f, 1f);
    }

    private float CalculateWeightedAverage(System.Collections.Generic.List<float> scores)
    {
        if (Weights.Count == 0)
        {
            // Equal weights if none specified - fall back to simple average
            return scores.Average();
        }

        float weightedSum = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < scores.Count; i++)
        {
            float weight = i < Weights.Count ? Weights[i] : 1f;
            weightedSum += scores[i] * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? Mathf.Clamp(weightedSum / totalWeight, 0f, 1f) : 0f;
    }

    #region Test Helpers

    /// <summary>
    /// Sets the operator mode. Primarily for testing.
    /// </summary>
    public void SetOperator(ConsiderationOperator op)
    {
        Operator = op;
    }

    /// <summary>
    /// Adds a child consideration. Primarily for testing.
    /// </summary>
    public void AddChild(UtilityConsideration consideration)
    {
        Considerations.Add(consideration);
    }

    /// <summary>
    /// Sets the weights array for WeightedAverage mode. Primarily for testing.
    /// </summary>
    public void SetWeights(float[] weights)
    {
        Weights.Clear();
        foreach (var w in weights)
        {
            Weights.Add(w);
        }
    }

    /// <summary>
    /// Sets the threshold for ThresholdGate mode. Primarily for testing.
    /// </summary>
    public void SetThreshold(float threshold)
    {
        Threshold = threshold;
    }

    #endregion
}
