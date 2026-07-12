namespace Jmodot.Core.AI.Navigation.Considerations;

using System.Collections.Generic;
using System.Linq;
using BB;
using Implementation.AI.Navigation;
using Implementation.AI.Navigation.Considerations;
using Implementation.Shared;
using Movement;
using SteeringModifiers;
using GColl = Godot.Collections;

/// <summary>
/// How a consideration's danger contributes to steering exclusion. <see cref="Soft"/> danger
/// only subtracts interest; <see cref="Hard"/> danger at or above the mask threshold excludes
/// the bin outright (a bool-channel veto that never travels in float space).
/// </summary>
public enum SteeringConstraintMode
{
    Soft,
    Hard,
}

/// <summary>
/// The abstract base class for any environmental consideration. Its purpose is to
/// evaluate the current world state (via the SteeringDecisionContext) and produce a
/// dictionary of signed [-1,1] directional scores. The base pipeline clamps those scores
/// to the contract, propagates them (optional neighbor bleed), applies subjective modifiers,
/// re-clamps, derives the Hard mask, and routes each score into a <see cref="SteeringContextMap"/>'s
/// Interest / Danger / HardMask channels — with <see cref="Weight"/> applied strictly AFTER the
/// final clamp so it is the only cross-consideration magnitude knob.
///
/// Derived classes only implement CalculateBaseScores; clamp / propagation / modifiers / routing
/// are handled centrally in Evaluate().
/// </summary>
[GlobalClass, Tool]
public abstract partial class BaseAIConsideration3D : Resource
{
    /// <summary>
    /// The single cross-consideration magnitude knob. Applied AFTER the final clamp, so a
    /// consideration can never contribute more than Weight to a bin regardless of propagation
    /// headroom or modifier amplification.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 5.0, 0.05")]
    public float Weight { get; private set; } = 1.0f;

    /// <summary>
    /// Soft danger only subtracts interest; Hard danger at or above the mask threshold excludes
    /// the bin from steering entirely.
    /// </summary>
    [Export] public SteeringConstraintMode ConstraintMode { get; private set; } = SteeringConstraintMode.Soft;

    /// <summary>
    /// For Hard considerations, the post-clamp danger magnitude at or above which a bin is masked.
    /// Ignored when ConstraintMode is Soft.
    /// </summary>
    [Export(PropertyHint.Range, "0.05, 1.0, 0.05")]
    private float _hardMaskThreshold = 0.5f;

    /// <summary>
    /// A list of modifiers that can alter the objective scores of this consideration,
    /// allowing an AI's personality (Affinities) to influence its low-level behavior.
    /// </summary>
    [Export] private GColl.Array<SteeringConsiderationModifier3D> _modifiers = new();

    /// <summary>
    /// Optional propagation config that smooths scores by bleeding them to neighboring
    /// directions. Default config: NeighborCount=2, DiminishWeight=0.5.
    /// Set to null to disable propagation entirely.
    /// </summary>
    [ExportGroup("Score Propagation")] [Export]
    private SteeringPropagationConfig? _propagation;

    /// <summary>Latch so an out-of-contract base score warns once per instance, not every frame.</summary>
    private bool _contractViolationLogged;

    /// <summary>
    /// Called once by the AISteeringProcessor during initialization, letting a consideration
    /// perform optional setup. The base implementation is a no-op — propagation derives its
    /// ordered ring directly from the DirectionSet3D at evaluation time. The virtual is retained
    /// as the processor's per-consideration setup seam; overrides need not call base.
    /// </summary>
    /// <param name="directions">The DirectionSet3D used by the agent.</param>
    public virtual void Initialize(DirectionSet3D directions)
    {
    }

    /// <summary>
    /// The primary evaluation method. Calculates base scores, clamps them to the [-1,1] contract
    /// (warn-once on violation), applies propagation and subjective modifiers, re-clamps, derives
    /// the Hard mask, and routes each score into the context map's Interest / Danger / HardMask
    /// channels scaled by <see cref="Weight"/>.
    /// </summary>
    public void Evaluate(SteeringDecisionContext3D context3D, IBlackboard blackboard,
        DirectionSet3D directions, SteeringContextMap map)
    {
        // 1. Calculate the raw, objective scores for this consideration.
        var baseScores = CalculateBaseScores(directions, context3D, blackboard);

        // 2. Contract clamp: base scores MUST be signed [-1,1]. Violations warn once, then clamp.
        foreach (var key in baseScores.Keys.ToList())
        {
            float raw = baseScores[key];
            if (raw < -1f || raw > 1f)
            {
                if (!_contractViolationLogged)
                {
                    JmoLogger.Warning(this,
                        $"Consideration '{ResourceName}' produced score {raw} for direction {key}, " +
                        "outside the [-1,1] output contract; clamping. Further violations from this instance are suppressed.");
                    _contractViolationLogged = true;
                }
                baseScores[key] = Mathf.Clamp(raw, -1f, 1f);
            }
        }

        // 3. Apply propagation smoothing (symmetric neighbor bleed) along the set's ordered ring.
        if (_propagation != null)
        {
            SteeringPropagation.PropagateScores(
                baseScores, directions,
                _propagation.NeighborCount, _propagation.DiminishWeight);
        }

        // 4. Apply all subjective modifiers to the scores.
        foreach (var modifier in _modifiers)
        {
            modifier.Modify(ref baseScores, context3D, blackboard);
        }

        // 5. Final clamp + mask derivation + channel routing. The clamp sits BEFORE the Weight
        // multiply, so propagation headroom / modifier amplification can never push a contribution
        // past Weight, and mask thresholds always compare true normalized [0,1] values.
        foreach (var kvp in baseScores)
        {
            int i = directions.IndexOfOrdered(kvp.Key);
            if (i < 0) { continue; }

            float v = Mathf.Clamp(kvp.Value, -1f, 1f);

            if (ConstraintMode == SteeringConstraintMode.Hard && -v >= _hardMaskThreshold)
            {
                map.HardMask[i] = true;
            }

            if (v > 0f) { map.Interest[i] += v * Weight; }
            else if (v < 0f) { map.Danger[i] += -v * Weight; }
        }
    }

    // TRANSITIONAL (deleted in P5 consideration sweep): flattens the channel map to the legacy
    // signed dict so un-migrated considerations + direct callers stay green.
    public void Evaluate(SteeringDecisionContext3D context3D, IBlackboard blackboard,
        DirectionSet3D directions, ref Dictionary<Vector3, float> scores)
    {
        var map = new SteeringContextMap(directions.OrderedDirections);
        Evaluate(context3D, blackboard, directions, map);
        for (int i = 0; i < map.Bins.Count; i++)
        {
            var bin = map.Bins[i];
            if (scores.ContainsKey(bin))
            {
                scores[bin] += map.Interest[i] - map.Danger[i];
            }
        }
    }

    /// <summary>
    /// Child classes MUST implement this method. It contains the core logic for calculating
    /// the raw directional scores before any personality-driven modifications are applied.
    /// Scores MUST be signed and bounded to [-1,1] (positive = interest, negative = danger).
    /// </summary>
    protected abstract Dictionary<Vector3, float> CalculateBaseScores(
        DirectionSet3D directions, SteeringDecisionContext3D context3D, IBlackboard blackboard);

    #region Test Helpers
#if TOOLS
    internal void SetPropagation(SteeringPropagationConfig? config) => _propagation = config;
    internal void SetWeight(float weight) => Weight = weight;
    internal void SetConstraintMode(SteeringConstraintMode mode) => ConstraintMode = mode;
    internal void SetHardMaskThreshold(float threshold) => _hardMaskThreshold = threshold;
    internal void SetModifiers(GColl.Array<SteeringConsiderationModifier3D> modifiers) => _modifiers = modifiers;
#endif
    #endregion
}
