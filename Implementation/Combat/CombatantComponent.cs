using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Health;
using Jmodot.Core.Health;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

using Core.Combat.Reactions;
using Status;
using Visual.Effects;
using System.Linq;

/// <summary>
/// The central brain for combat interactions on an entity.
/// Receives payloads from the Hurtbox and delegates logic to Effects.
/// Acts as a Service Locator for combat-related components.
/// </summary>
[GlobalClass]
public partial class CombatantComponent : Node, IComponent, ICombatant, IBlackboardProvider
{
    #region IBlackboardProvider Implementation
    /// <summary>
    /// Auto-registers this component with the blackboard during EntityNodeComponentsInitializer.
    /// </summary>
    public (StringName Key, object Value)? Provision => (BBDataSig.CombatantComponent, this);
    #endregion

    #region Dependencies
    /// <summary>
    /// Resolved via Inspector or Blackboard fallback in Initialize().
    /// Not [RequiredExport] because spells may not Inspector-assign these.
    /// </summary>
    [Export] public HealthComponent? Health { get; private set; }

    /// <summary>
    /// Resolved via Inspector or Blackboard fallback in Initialize().
    /// Not [RequiredExport] because spells may not Inspector-assign these.
    /// </summary>
    [Export] public StatusEffectComponent? StatusComponent { get; private set; }
    #endregion

    // The "Universal" Event.
    // Listeners use pattern matching: if (result is DamageResult dr) ...
    public event Action<CombatResult> CombatResultEvent = delegate { };

    /// <summary>
    /// Cached VisualEffectController resolved on first access. Avoids a LINQ child-tree scan +
    /// FirstOrDefault allocation per ApplyEffect call — relevant for tick/DoT effects that fire on
    /// a cadence. Assumes the controller is parented as a child of this component at scene load and
    /// doesn't migrate at runtime; if that assumption changes, invalidate the cache on
    /// ChildEnteredTree / ChildExitingTree.
    /// </summary>
    private VisualEffectController? _cachedVisualController;

    #region Node Overrides
    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) { return; }
    }
    #endregion


    #region ICombatant Implementation
    public Node OwnerNode => GetOwner();
    public Jmodot.Core.AI.BB.IBlackboard Blackboard { get; private set; } = null!;

    public void ProcessPayload(IAttackPayload payload, HitContext context, float incomingMagnitudeScale = 1.0f)
    {
        if (payload.Effects == null || payload.Effects.Count == 0)
        {
            return;
        }
        var scope = Reporter.BeginPayload(context, incomingMagnitudeScale);
        try
        {
            foreach (var effect in payload.Effects)
            {
                scope.Collect(ApplyEffectCore(effect, context, incomingMagnitudeScale));
            }
        }
        finally
        {
            Reporter.EndPayload(scope);
        }
    }

    /// <summary>
    /// Breaking this out into a single function to allow to single effect applications (from status runners or elsewhere)
    /// </summary>
    /// <param name="effect"></param>
    /// <param name="context"></param>
    public void ApplyEffect(ICombatEffect effect, HitContext context, float incomingMagnitudeScale = 1.0f)
        => ApplyEffectCore(effect, context, incomingMagnitudeScale);

    private CombatResult? ApplyEffectCore(ICombatEffect effect, HitContext context, float incomingMagnitudeScale)
    {
        if (effect == null)
        {
            return null;
        }

        // 1. EXECUTE
        // The Effect contains the logic (Calculation, Armor Check, Health Modification).
        // The Combatant just provides the context (itself).
        CombatResult? result = effect.Apply(this, context, incomingMagnitudeScale);

        // Stamped here, once, so no effect has to remember it: every result of a held contact is
        // sustained, whatever effect produced it.
        if (context.Continuous && result is { Sustained: false })
        {
            result = result with { Sustained = true };
        }

        // 2. VISUALS
        // Trigger any associated visual effect on the target. Controller is cached on first access
        // (see _cachedVisualController XML doc for assumptions).
        if (effect.Visual != null)
        {
             _cachedVisualController ??= GetUnderlyingNode().GetChildrenOfType<VisualEffectController>().FirstOrDefault();
             _cachedVisualController?.PlayEffect(effect.Visual);
        }

        // 3. REPORT
        // If something actually happened, broadcast it.
        if (result != null)
        {
            CombatResultEvent?.Invoke(result);
        }

        return result;
    }

    #endregion

    private ImpactReporter? _reporter;
    private ImpactReporter Reporter => _reporter ??= new ImpactReporter(this);

    /// <summary>
    /// Raises <paramref name="report"/> on <see cref="CombatResultEvent"/> and hands it to the scene sink,
    /// the one path every impact report takes out of this combatant.
    /// </summary>
    internal void PublishReport(ImpactReport report)
    {
        CombatResultEvent?.Invoke(report);
        HitReportSeam.Sink?.Receive(report);
    }

    /// <summary>
    /// Holds this body's death report until the returned scope is disposed. Wrap a damage call and its
    /// <see cref="ReportImpact"/> in it so a lethal blow pairs with the report that follows it.
    /// </summary>
    public IDisposable HoldDeathReport() => Reporter.HoldDeath();

    /// <summary>
    /// Publishes the hit report for this body striking a surface; the caller has already applied the damage.
    /// </summary>
    public void ReportImpact(in ImpactInfo info, object source, float healthBefore, float requestedDamage)
        => Reporter.ReportImpact(info, source, healthBefore, requestedDamage);

    /// <summary>
    /// Called by StatusEffectComponent when a runner finishes.
    /// Bridges the gap between the Status system and the generic Reaction system.
    /// </summary>
    private void StatusRemoved(StatusRunner runner, bool wasDispelled)
    {
        var result = new StatusExpiredResult
        {
            Source = null, // Expiration is internal
            Target = OwnerNode,
            Tags = runner.Tags,
            WasDispelled = wasDispelled
        };

        CombatResultEvent?.Invoke(result);
    }

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        Blackboard = bb;

        // If dependencies aren't assigned in Inspector, try to find them from blackboard
        // Using TryGet allows these to be optional - spells have Health but no StatusEffects
        if (Health == null)
        {
            bb.TryGet<HealthComponent>(BBDataSig.HealthComponent, out var health);
            Health = health;
        }

        if (StatusComponent == null)
        {
            bb.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComponent);
            StatusComponent = statusComponent;
        }

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        SubscribeReportHooks();

        if (StatusComponent != null)
        {
            StatusComponent.StatusRemoved += StatusRemoved;
        }
    }
    public event Action Initialized = delegate { };

    // A health component clears its subscribers when a pooled owner resets, so this runs again on the next life.
    private void SubscribeReportHooks()
    {
        if (Health == null) { return; }
        Health.OnDamaged -= Reporter.OnDamaged;
        Health.OnDamaged += Reporter.OnDamaged;
        Health.OnDied -= Reporter.OnDied;
        Health.OnDied += Reporter.OnDied;
    }

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Raises CombatResultEvent directly, bypassing ProcessPayload/ApplyEffect/Hurtbox.
    /// Lets integration tests synthesize a hit on this entity to exercise CombatLogger
    /// → CombatLog wiring and downstream BTConditions without a full attack pipeline.
    /// </summary>
    internal void _TestRaiseResult(CombatResult result) => CombatResultEvent?.Invoke(result);
#endif
    #endregion
}
