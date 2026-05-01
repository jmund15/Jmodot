namespace Jmodot.Core.Combat.Status;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Identification;
using Jmodot.Implementation;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Combat.Status;
using Jmodot.Implementation.Shared;
using GCol = Godot.Collections;

/// <summary>
/// Designer-tunable contagion config for any <see cref="StatusRunner"/>. When attached
/// (via <see cref="StatusRunner.SpreadConfig"/>), the host's <see cref="Implementation.Combat.StatusEffectComponent"/>
/// periodically calls <see cref="TryEvaluate"/> to decide whether to spawn a sibling
/// instance of the same status type on a nearby qualifying target.
///
/// All decision logic + filtering rules live HERE on the Resource — runner only carries
/// per-instance state, component only drives the loop. Future SpreadConfig variants encapsulate
/// their rules without modifying StatusRunner or StatusEffectComponent.
/// </summary>
[GlobalClass, Tool]
public partial class StatusSpreadConfig : Resource
{
    /// <summary>
    /// Per-evaluation chance to attempt a spread (rolled once per evaluation tick).
    /// Combined with <see cref="ChanceFalloffByGeneration"/> if set.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChancePerEvaluation { get; private set; } = 0.20f;

    /// <summary>
    /// World-units radius the component queries for candidates around the host's target.
    /// </summary>
    [Export] public float Range { get; private set; } = 2.5f;

    /// <summary>
    /// Maximum number of new targets infected in a single evaluation. With many candidates
    /// in range, only this many will be chosen at random.
    /// </summary>
    [Export] public int MaxTargetsPerEvaluation { get; private set; } = 1;

    /// <summary>
    /// Optional curve that scales <see cref="ChancePerEvaluation"/> by the generation ratio
    /// (host.SpreadGeneration / MaxGenerations, normalized to [0,1]). Use to reduce probability
    /// at later generations so the contagion fizzles. Null = no falloff.
    /// </summary>
    [Export] public Curve? ChanceFalloffByGeneration { get; private set; }

    /// <summary>
    /// Maximum spread generation. Generation 0 = primary; 1..MaxGenerations-1 = spread.
    /// At gen >= MaxGenerations, evaluation always returns false (hard fizzle gate).
    /// </summary>
    [Export] public int MaxGenerations { get; private set; } = 3;

    /// <summary>
    /// Optional category filter on candidates — only candidates whose identity descends from
    /// (or equals) this category qualify. Null = no category filter.
    /// </summary>
    [Export] public Category? TargetCategory { get; private set; }

    /// <summary>
    /// Tags that DISQUALIFY a candidate. If a candidate's StatusEffectComponent already has
    /// any of these tags, it is filtered out. Use to prevent self-reinfection (e.g., a target
    /// already burning shouldn't be a spread candidate).
    /// </summary>
    [Export] public GCol.Array<CombatTag> ExcludeIfPresent { get; private set; } = new();

    /// <summary>
    /// Seconds between consecutive spread evaluations for a single runner. The component's
    /// per-frame driver advances each runner's accumulator and triggers an evaluation when
    /// the accumulator crosses this threshold (overshoot is preserved so cadence doesn't drift
    /// on slow frames). Per-config, not global — a fast-spreading panic and a slow burn can
    /// coexist on the same entity with different cadences.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,30,0.05")]
    public float EvaluationInterval { get; private set; } = 1.0f;

    /// <summary>
    /// Total cap on evaluation attempts per runner over its lifetime. -1 = unlimited.
    /// Use to bound contagion budget independent of duration (e.g., a 5s burn that should
    /// only attempt to spread 4 times regardless of tick rate).
    /// </summary>
    [Export] public int MaxEvaluations { get; private set; } = -1;

    /// <summary>
    /// Decides whether a spread evaluation should fire and pick targets.
    /// All decision logic lives here so future SpreadConfig variants encapsulate their rules
    /// without changing the runner or component.
    /// </summary>
    public bool TryEvaluate(StatusRunner host, IEnumerable<ICombatant> nearbyCandidates, out List<ICombatant> picks)
    {
        picks = new List<ICombatant>();

        if (!ShouldFireByGeneration(host.SpreadGeneration)) { return false; }

        float effectiveChance = GetEffectiveChance(host.SpreadGeneration);
        if (JmoRng.GetRndFloat() >= effectiveChance) { return false; }

        var candidates = FilterCandidates(nearbyCandidates);
        if (candidates.Count == 0) { return false; }

        for (int i = 0; i < MaxTargetsPerEvaluation && candidates.Count > 0; i++)
        {
            int idx = JmoRng.GetRndInt(candidates.Count);
            picks.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
        return picks.Count > 0;
    }

    /// <summary>
    /// True when the generation gate allows a fire (currentGeneration < MaxGenerations).
    /// Public for direct testing.
    /// </summary>
    public bool ShouldFireByGeneration(int currentGeneration)
        => currentGeneration < MaxGenerations;

    /// <summary>
    /// True when the runner's evaluation count is below <see cref="MaxEvaluations"/>
    /// (or the cap is disabled with -1). Honored by both <c>TickSpread</c> and the
    /// test-friendly <c>EvaluateSpread</c> one-shot path.
    /// </summary>
    public bool CanEvaluate(int currentEvaluationCount)
        => MaxEvaluations < 0 || currentEvaluationCount < MaxEvaluations;

    /// <summary>
    /// Effective per-evaluation chance after applying the falloff curve. Public for direct testing.
    /// </summary>
    public float GetEffectiveChance(int currentGeneration)
    {
        if (ChanceFalloffByGeneration == null || MaxGenerations <= 0)
        {
            return ChancePerEvaluation;
        }

        float t = Mathf.Clamp((float)currentGeneration / MaxGenerations, 0f, 1f);
        float falloff = ChanceFalloffByGeneration.Sample(t);
        return ChancePerEvaluation * falloff;
    }

    /// <summary>
    /// Applies category + exclude-tag filters. Public for direct testing of the predicate logic.
    /// </summary>
    public List<ICombatant> FilterCandidates(IEnumerable<ICombatant> candidates)
    {
        var result = new List<ICombatant>();
        foreach (var candidate in candidates)
        {
            if (candidate == null) { continue; }
            if (TargetCategory != null && !MatchesCategory(candidate)) { continue; }
            if (ExcludeIfPresent.Count > 0 && HasAnyExcludedTag(candidate)) { continue; }
            result.Add(candidate);
        }
        return result;
    }

    private bool MatchesCategory(ICombatant candidate)
    {
        if (candidate.OwnerNode is not IIdentifiable identifiable) { return false; }
        var identity = identifiable.GetIdentity();
        if (identity?.Categories == null) { return false; }
        foreach (var cat in identity.Categories)
        {
            if (cat?.IsOrDescendsFrom(TargetCategory!) == true) { return true; }
        }
        return false;
    }

    private bool HasAnyExcludedTag(ICombatant candidate)
    {
        // Look up candidate's StatusEffectComponent via blackboard. If absent, candidate
        // cannot have any status tags by definition — they are NOT excluded.
        if (candidate.Blackboard == null) { return false; }
        if (!candidate.Blackboard.TryGet<StatusEffectComponent>(Implementation.AI.BB.BBDataSig.StatusEffects, out var sec)
            || sec == null)
        {
            return false;
        }

        foreach (var tag in ExcludeIfPresent)
        {
            if (tag != null && sec.HasTag(tag)) { return true; }
        }
        return false;
    }

#if TOOLS
    /// <summary>
    /// Test helper — sets private export fields without needing .tres files.
    /// </summary>
    internal void SetTestValues(
        float chancePerEvaluation,
        int maxGenerations,
        int maxTargetsPerEvaluation = 1,
        Curve? falloff = null,
        Category? targetCategory = null,
        GCol.Array<CombatTag>? excludeIfPresent = null,
        float range = 2.5f,
        float evaluationInterval = 1.0f,
        int maxEvaluations = -1)
    {
        ChancePerEvaluation = chancePerEvaluation;
        MaxGenerations = maxGenerations;
        MaxTargetsPerEvaluation = maxTargetsPerEvaluation;
        ChanceFalloffByGeneration = falloff;
        TargetCategory = targetCategory;
        if (excludeIfPresent != null) { ExcludeIfPresent = excludeIfPresent; }
        Range = range;
        EvaluationInterval = evaluationInterval;
        MaxEvaluations = maxEvaluations;
    }
#endif
}
