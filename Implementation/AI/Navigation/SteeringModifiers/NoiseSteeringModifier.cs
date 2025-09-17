#region

using System.Collections.Generic;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.Navigation.SteeringModifiers;

#endregion

namespace Jmodot.Implementation.AI.Navigation.SteeringModifiers;

/// <summary>
///     Modifies the output of a steering consideration by applying a time-varying noise value.
///     This can make an AI's behavior less predictable and more organic, simulating shifts in
///     mood or focus. For example, it can make an "avoid walls" consideration sometimes more
///     potent (cautious AI) and sometimes less potent (brave AI).
/// </summary>
[GlobalClass]
public partial class NoiseSteeringModifier : SteeringConsiderationModifier
{
    [ExportGroup("Influence Clamping")] [Export(PropertyHint.Range, "0.0, 2.0, 0.05")]
    private float _baseInfluence = 1.0f;

    [Export(PropertyHint.Range, "1.0, 3.0, 0.05")]
    private float _maxInfluence = 1.5f;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    private float _minInfluence = 0.5f;

    [ExportGroup("Noise Configuration")] [Export]
    private FastNoiseLite _noise = null!;

    [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
    private float _noiseIntensity = 0.2f;

    [Export] private float _noiseTimeScale = 0.5f;

    public override void Modify(ref Dictionary<Vector3, float> scores, DecisionContext context, IBlackboard blackboard)
    {
        if (_noise == null) return;

        // Get a noise value between -1 and 1
        var time = (float)Time.GetUnixTimeFromSystem() * _noiseTimeScale;
        var noiseValue = _noise.GetNoise1D(time);

        // Calculate the final influence multiplier
        var influence = _baseInfluence + noiseValue * _noiseIntensity;
        var clampedInfluence = Mathf.Clamp(influence, _minInfluence, _maxInfluence);

        // Apply the multiplier to all scores produced by the consideration
        var keys = new List<Vector3>(scores.Keys);
        foreach (var key in keys) scores[key] *= clampedInfluence;
    }
}