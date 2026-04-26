namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Core.Combat;
using Core.Identification;
using Core.Pooling;
using Implementation.Health;
using Shared;

/// <summary>
/// AI-architecture-agnostic damage application for force-driven impacts. Subscribes to
/// <see cref="ImpactDetector"/> events, gates on <see cref="ForceControlLossDetector.IsControlLost"/>,
/// and applies <see cref="ImpactDamageProfile"/>-derived damage to the actor's
/// <see cref="HealthComponent"/>. HSM and BT consumers inherit damage handling for free —
/// they only need to manage state transitions, never damage logic.
/// </summary>
[GlobalClass]
public partial class ForceImpactDamageApplier : Node, IPoolResetable
{
    [Export] public ImpactDamageProfile? DamageProfile { get; set; }

    private ImpactDetector? _detector;
    private ForceControlLossDetector? _controlDetector;
    private HealthComponent? _health;
    private Node3D? _self;

    /// <summary>
    /// Per-collider identity cache. Bounded by ControlLost windows: cleared on
    /// <see cref="ForceControlLossDetector.ControlRegained"/> so a recycled
    /// <c>GetInstanceId()</c> across capture windows can never serve a stale identity.
    /// </summary>
    private readonly Dictionary<ulong, IIdentifiable?> _identityCache = new();

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
        _identityCache.Clear();

        _detector.Impacted += OnImpacted;
        _controlDetector.ControlRegained += OnControlRegained;
    }

    public void OnPoolReset()
    {
        _identityCache.Clear();
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

        if (_controlDetector != null)
        {
            _controlDetector.ControlRegained -= OnControlRegained;
        }
    }

    private void OnControlRegained()
    {
        _identityCache.Clear();
    }

    private void OnImpacted(ImpactInfo info)
    {
        if (_controlDetector?.IsControlLost != true)
        {
            return;
        }

        if (_health == null || DamageProfile == null)
        {
            return;
        }

        if (_self != null && info.Collider == _self)
        {
            return;
        }

        // Identity cache (cleared on ControlRegained — bounded per capture window).
        var id = info.Collider.GetInstanceId();
        if (!_identityCache.TryGetValue(id, out var _))
        {
            info.Collider.TryGetFirstChildOfInterface<IIdentifiable>(out IIdentifiable? identifiable);
            _identityCache[id] = identifiable;
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
            $"Impact damage: {damage:F1} at speed {info.Speed:F1} from {source.GetType().Name}");
    }
}
