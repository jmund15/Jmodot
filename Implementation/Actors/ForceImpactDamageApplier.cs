namespace Jmodot.Implementation.Actors;

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
/// "is launched" / "is captured" gates — <c>ImpactDamageProfile.VelocityThreshold</c> is
/// the only gate, and it lives in data. HSM/BT consumers inherit damage for free; adding
/// a new movement-control concept is purely a state-side change.
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
    [Export] public float SourceAttributionWindowSeconds { get; private set; } = 2.0f;

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

        if (_health == null || DamageProfile == null)
        {
            return;
        }

        var damage = DamageProfile.CalculateDamage(info.Speed);
        if (damage <= 0f)
        {
            // VelocityThreshold inside the profile IS the gate — no regime flag needed.
            return;
        }

        var source = ResolveSource(info);
        _health.TakeDamage(damage, source);

        ApplyVelocityLoss(damage, info.Collider);
    }

    /// <summary>
    /// Delegates to <see cref="SourceAttributionResolver.Resolve"/> — three-step chain:
    /// most-recent KnockbackResult in CombatLog → dominant force from receiver → collider.
    /// Extracted as a pure static so chain ordering, window expiry, and null-degradation
    /// paths are unit-tested independently of this Node's lifecycle.
    /// </summary>
    private Node? ResolveSource(ImpactInfo info)
        => SourceAttributionResolver.Resolve(
            info, _combatLog, _forceReceiver, _self, SourceAttributionWindowSeconds);

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
}
