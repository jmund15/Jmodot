namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Actors;
using Core.AI.BB;
using Core.Combat;
using Core.Combat.EffectDefinitions;
using Core.Combat.Reactions;
using Core.Pooling;
using Core.Shared.Attributes;
using Core.Stats;
using Implementation.AI.BB;
using Implementation.Combat;
using Implementation.Health;
using Shared;

/// <summary>
/// AI-architecture-agnostic damage application for force-driven impacts. Subscribes to
/// <see cref="ImpactDetector"/> events and applies <see cref="ImpactDamageProfile"/>-derived
/// damage. Source attribution walks a three-step fallback chain: most recent transient
/// knockback (from <see cref="CombatLog"/>), then dominant sustained force (from
/// <see cref="ExternalForceReceiver3D"/>), then the collider itself.
/// </summary>
/// <remarks>
/// <para>
/// Pure function of physics + a data profile + a source lookup. No regime flags, no
/// "is launched" / "is captured" gates — the gates are <c>ImpactDamageProfile.VelocityThreshold</c>
/// (severity, in data) and <see cref="RequireExternalCause"/> (attribution evidence, in data).
/// The attribution gate reuses the source lookup: an impact with no recent knockback and no
/// dominant sustained force was self-propelled, so it is not a force-driven impact. HSM/BT
/// consumers inherit damage for free; adding a new movement-control concept is purely a
/// state-side change.
/// </para>
/// <para>
/// Also applies a post-damage velocity-loss step (<c>ImpactVelocityLoss</c>): the launcher
/// loses kinetic energy proportional to the damage it dealt, scaled by the target's
/// absorption coefficient. Enables Wind-Blast chain mechanics.
/// </para>
/// <para>
/// Re-entrant: <see cref="Initialize"/> tears down prior subscriptions before re-binding,
/// safe to call repeatedly (pool reuse, scene reload).
/// </para>
/// </remarks>
[GlobalClass]
public partial class ForceImpactDamageApplier : Node, IPoolResetable
{
    [Export, RequiredExport] public ImpactDamageProfile DamageProfile { get; set; } = null!;

    [ExportGroup("Source Attribution")]
    /// <summary>
    /// Time window (seconds) in which a logged knockback is still considered the cause of
    /// an impact. Outside this window, attribution falls back to the dominant sustained
    /// force or the collider itself. Tunable per actor.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,10.0,0.1,suffix:s")] public float SourceAttributionWindowSeconds { get; private set; } = 2.0f;

    /// <summary>
    /// When true (default), damage applies only to impacts with an attributable external
    /// cause — a recent knockback or a dominant sustained force. Self-propelled collisions
    /// (attack lunges, leap landings, voluntary falls) resolve to the collider fallback and
    /// are skipped, so an actor never damages itself with its own movement. Disable for
    /// actors that should take raw kinetic collision damage regardless of cause.
    /// </summary>
    [Export] public bool RequireExternalCause { get; private set; } = true;

    [ExportGroup("Velocity Loss")]
    /// <summary>Launcher mass used to convert N·s → Δv. Null → 1.0 (preserves pre-mass-aware feel).</summary>
    [Export] public BaseFloatValueDefinition? Mass { get; private set; }
    /// <summary>Target absorption coefficient (0..1, clamped). Null → 0.5 default.</summary>
    [Export] public BaseFloatValueDefinition? Absorption { get; private set; }

    private ImpactDetector? _detector;
    private HealthComponent? _health;
    private Node3D? _self;
    private CombatLog? _combatLog;
    private ExternalForceReceiver3D? _forceReceiver;
    private IReadOnlyList<IImpactDamageGate> _gates = new List<IImpactDamageGate>();

    private IStatProvider? _launcherStatProvider;
    private bool _launcherStatProviderResolved;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        base._Ready();
    }

    public void Initialize(
        ImpactDetector detector,
        HealthComponent health,
        Node3D self,
        IBlackboard bb)
    {
        Teardown();

        _detector = detector;
        _health = health;
        _self = self;
        _launcherStatProviderResolved = false;
        _launcherStatProvider = null;

        // Optional siblings: degrade gracefully when not BB-published.
        bb.TryGet<CombatLog>(BBDataSig.CombatLog, out _combatLog);
        bb.TryGet<ExternalForceReceiver3D>(BBDataSig.ExternalForceReceiver, out _forceReceiver);

        // Capability gates (invulnerability window, damage-absorption shield, …) veto impact
        // damage per-impact. Direct siblings only — a gate is a peer component, not something
        // inherited from a nested subtree. Re-resolved on every Initialize to honor the
        // pool-reuse/rebind contract.
        _gates = self.GetChildrenOfInterface<IImpactDamageGate>(includeSubChildren: false).ToList();

        _detector.Impacted += OnImpacted;
    }

    public void OnPoolReset()
    {
        _launcherStatProviderResolved = false;
        _launcherStatProvider = null;
    }

    public override void _ExitTree()
    {
        Teardown();
        base._ExitTree();
    }

    private void Teardown()
    {
        if (_detector != null)
        {
            _detector.Impacted -= OnImpacted;
        }
    }

    private void OnImpacted(ImpactInfo info)
    {
        if (_self != null && info.Collider == _self)
        {
            return;
        }

        // A denying gate absorbs the whole impact — skip both damage and the velocity-loss step.
        foreach (var gate in _gates)
        {
            if (!gate.AllowImpactDamage(info))
            {
                return;
            }
        }

        if (_health == null || DamageProfile == null)
        {
            return;
        }

        var damage = DamageProfile.CalculateDamage(info.Speed);
        if (damage <= 0f)
        {
            // VelocityThreshold inside the profile gates severity; the attribution gate
            // below decides whether the impact counts as force-driven at all.
            return;
        }

        // Three-step attribution chain: most-recent KnockbackResult in CombatLog →
        // dominant force from receiver → collider fallback. Extracted as a pure static
        // (SourceAttributionResolver) so chain ordering, window expiry, and
        // null-degradation paths are unit-tested independently of this Node's lifecycle.
        var (source, cause) = SourceAttributionResolver.ResolveWithCause(
            info, _combatLog, _forceReceiver, _self, SourceAttributionWindowSeconds);

        if (RequireExternalCause && cause == ImpactCause.ColliderFallback)
        {
            // No external evidence — the actor's own movement caused this collision
            // (attack lunge, leap landing, voluntary fall). Not a force-driven impact.
            return;
        }

        _health.TakeDamage(damage, source);

        ApplyVelocityLoss(damage, info.Collider);
    }

    private void ApplyVelocityLoss(float damage, Node3D target)
    {
        var mass = Mass?.ResolveFloatValue(GetLauncherStatProvider()) ?? 1f;
        var absorption = Absorption?.ResolveFloatValue(WalkForCombatantStatProvider(target)) ?? 0.5f;

        if (mass <= 0f)
        {
            JmoLogger.Warning(this, $"VelocityLoss skipped: invalid mass={mass:F2}.");
            return;
        }

        if (_self is CharacterBody3D cb)
        {
            cb.Velocity = ImpactVelocityLoss.ComputeNewVelocity(cb.Velocity, damage, absorption, mass);
        }
        else if (_self is RigidBody3D rb)
        {
            rb.LinearVelocity = ImpactVelocityLoss.ComputeNewVelocity(rb.LinearVelocity, damage, absorption, mass);
        }
    }

    private IStatProvider? GetLauncherStatProvider()
    {
        if (_launcherStatProviderResolved)
        {
            return _launcherStatProvider;
        }
        _launcherStatProviderResolved = true;
        if (_self == null)
        {
            return null;
        }
        _launcherStatProvider = WalkForCombatantStatProvider(_self);
        return _launcherStatProvider;
    }

    /// <summary>
    /// Walks <paramref name="start"/> + ancestors for any <see cref="ICombatant"/> whose
    /// blackboard publishes <see cref="BBDataSig.Stats"/>. Loose coupling — works for any
    /// future entity that implements ICombatant + publishes stats. Returns null if no stat
    /// provider found.
    /// </summary>
    private static IStatProvider? WalkForCombatantStatProvider(Node? start)
    {
        for (var n = start; n != null; n = n.GetParent())
        {
            if (n is ICombatant combatant
                && combatant.Blackboard.TryGet<IStatProvider>(BBDataSig.Stats, out var sp)
                && sp != null)
            {
                return sp;
            }
        }
        return null;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRequireExternalCauseForTesting(bool value) => RequireExternalCause = value;
#endif
    #endregion
}
