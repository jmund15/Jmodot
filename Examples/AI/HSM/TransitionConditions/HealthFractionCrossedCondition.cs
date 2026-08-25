namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.AI.HSM;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Health;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// Fires when the current health fraction crosses any configured threshold downward.
/// The condition is stateless, so a passing check can re-fire while the same damage remains
/// in the CombatLog; consumers must keep the hurt state's minimum duration longer than
/// <see cref="WindowSeconds"/>. This is an HSM transition surface, distinct from the
/// <c>HealthRatioCondition : BTCondition</c> surface rather than a parallel abstraction.
/// A missing health component or CombatLog is a configuration error and throws — an omitted
/// logger must never read as "no crossing".
/// </summary>
[GlobalClass, Tool]
public partial class HealthFractionCrossedCondition : CombatLogCondition
{
    /// <summary>The complete set of downward health-fraction thresholds for this transition.</summary>
    [Export]
    public Godot.Collections.Array<float> ThresholdFractions { get; set; } = new();

    /// <summary>The CombatLog lookback window used to reconstruct previous health.</summary>
    [Export(PropertyHint.Range, "0.01,10,0.01")]
    public float WindowSeconds { get; set; } = 0.25f;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet(BBDataSig.HealthComponent, out IHealth? health) || health == null)
        {
            throw new ResourceConfigurationException(
                $"Required blackboard dependency '{BBDataSig.HealthComponent}' was not provided.", this);
        }

        // Both reads are hard deps for THIS condition: an omitted CombatLogger would otherwise
        // read as a legitimate "no crossing" every frame. The base keeps its warn-false posture
        // for siblings whose log read is genuinely optional.
        if (!bb.TryGet(BBDataSig.CombatLog, out CombatLog? log) || log == null)
        {
            throw new ResourceConfigurationException(
                $"Required blackboard dependency '{BBDataSig.CombatLog}' was not provided.", this);
        }

        return base.Check(agent, bb);
    }

    protected override bool CheckEvent(CombatLog log)
    {
        if (CurrentBlackboard == null
            || !CurrentBlackboard.TryGet(BBDataSig.HealthComponent, out IHealth? health)
            || health == null)
        {
            throw new ResourceConfigurationException(
                $"Required blackboard dependency '{BBDataSig.HealthComponent}' was not provided.", this);
        }

        var current = health.CurrentHealth;
        var max = health.MaxHealth;
        if (!(max > 0f) || !float.IsFinite(max) || !float.IsFinite(current) || !float.IsFinite(WindowSeconds)
            || WindowSeconds < 0f)
        {
            return false;
        }

        double damageSum = 0d;
        foreach (var result in log.GetAllCombatResultsWithinCombatTime<DamageResult>(WindowSeconds))
        {
            if (!float.IsFinite(result.FinalAmount))
            {
                return false;
            }

            damageSum += result.FinalAmount;
        }

        var previous = (double)current + damageSum;
        var currentFraction = (double)current / max;
        var previousFraction = previous / max;
        if (!double.IsFinite(previous) || !double.IsFinite(currentFraction) || !double.IsFinite(previousFraction))
        {
            return false;
        }

        foreach (var threshold in ThresholdFractions)
        {
            if (!float.IsFinite(threshold))
            {
                continue;
            }

            if (previousFraction > threshold && threshold >= currentFraction)
            {
                return true;
            }
        }

        return false;
    }
}
