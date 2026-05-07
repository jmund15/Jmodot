namespace Jmodot.Implementation.Actors;

using Godot;
using Core.AI.BB;
using Core.Combat;
using Core.Combat.EffectDefinitions;
using Core.Combat.Reactions;
using Core.Pooling;
using Core.Shared.Attributes;
using Core.Stats;
using Implementation.AI.BB;
using Implementation.Health;
using Shared;

/// <summary>
/// AI-architecture-agnostic damage application for force-driven impacts. Subscribes to
/// <see cref="ImpactDetector"/> events, gates on <see cref="ForceControlLossDetector.IsControlLost"/>,
/// and applies <see cref="ImpactDamageProfile"/>-derived damage to the actor's
/// <see cref="HealthComponent"/>. HSM and BT consumers inherit damage handling for free —
/// they only need to manage state transitions, never damage logic.
///
/// Also applies a post-damage velocity-loss step (see <see cref="ImpactVelocityLoss"/>): the
/// launcher loses kinetic energy proportional to the damage it dealt, scaled by the target's
/// absorption coefficient. This enables Wind-Blast chain mechanics — entities lose more
/// velocity into denser targets, less into glancing/low-absorption ones.
/// </summary>
/// <remarks>
/// Re-entrant: <see cref="Initialize"/> tears down prior subscriptions before re-binding,
/// so it is safe to call repeatedly (pool reuse, scene reload).
/// </remarks>
[GlobalClass]
public partial class ForceImpactDamageApplier : Node, IPoolResetable
{
    [Export, RequiredExport] public ImpactDamageProfile DamageProfile { get; set; } = null!;

    [ExportGroup("Velocity Loss")]
    /// <summary>Launcher mass used to convert N·s → Δv. Null → 1.0 (preserves pre-mass-aware feel).</summary>
    [Export] public BaseFloatValueDefinition? Mass { get; private set; }
    /// <summary>Target absorption coefficient (0..1, clamped). Null → 0.5 default.</summary>
    [Export] public BaseFloatValueDefinition? Absorption { get; private set; }

    private ImpactDetector? _detector;
    private ForceControlLossDetector? _controlDetector;
    private HealthComponent? _health;
    private Node3D? _self;

    // Launcher's IStatProvider — lazy-init at first use to sidestep init-order hazards
    // (Initialize signature doesn't take a Blackboard, so we resolve from the launcher tree
    // after it has settled into the scene).
    private IStatProvider? _launcherStatProvider;
    private bool _launcherStatProviderResolved;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        base._Ready();
    }

    public void Initialize(
        ImpactDetector detector,
        ForceControlLossDetector controlDetector,
        HealthComponent health,
        Node3D self)
    {
        Teardown();

        _detector = detector;
        _controlDetector = controlDetector;
        _health = health;
        _self = self;
        _launcherStatProviderResolved = false;
        _launcherStatProvider = null;

        _detector.Impacted += OnImpacted;
    }

    public void OnPoolReset()
    {
        // Stateless on the applier side; the detector + control detector own their own
        // pool-reset hooks via IPoolResetable. Reset the lazy-init cache so the next mount
        // re-walks for an IStatProvider on its (potentially new) launcher.
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
        if (_controlDetector?.IsControlLost != true)
        {
            return;
        }

        // _health is null only in test scenarios that bypass Initialize.
        // DamageProfile is [RequiredExport] + validated in _Ready, so it cannot be null here.
        if (_health == null)
        {
            return;
        }

        if (_self != null && info.Collider == _self)
        {
            return;
        }

        var damage = DamageProfile.CalculateDamage(info.Speed);
        if (damage <= 0f)
        {
            return;
        }

        // Attribute to the dominant force source (the wave, etc.) when available;
        // fall back to the collider if context is missing.
        var source = (object?)_controlDetector.CurrentContext?.DominantSource ?? info.Collider;
        _health.TakeDamage(damage, source);

        JmoLogger.Info(this,
            $"Impact damage: {damage:F1} at speed {info.Speed:F1} from {source?.GetType().Name ?? "unknown"}");

        // Post-damage velocity-loss step (Wind-Blast chain mechanic).
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
        // No else clause — if _self isn't a known regime, skip silently (legitimate path: tests, custom launchers).
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
