using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Pooling;
using Jmodot.Core.Stats;
using GCol = Godot.Collections;
using Godot;

namespace Jmodot.Implementation.Combat;

using AI.BB;

/// <summary>
/// 2D twin of <see cref="HitboxComponent3D"/>. A generic collision sensor that
/// delivers attack payloads to Hurtboxes.
///
/// Responsibilities:
/// - Detect Overlaps (AreaEntered).
/// - Debounce (Prevent multi-hits on the same frame/attack cycle).
/// - Deliver Payload (Call Hurtbox.ProcessHit).
///
/// Modes:
/// - Standard: Hits target once per StartAttack().
/// - Continuous: Hits targets repeatedly based on TickInterval.
///
/// Registers on the shared BBDataSig.HitboxComponent key.
/// </summary>
[GlobalClass]
public partial class HitboxComponent2D : Area2D, IComponent, IBlackboardProvider, IPoolResetable
{
    #region IBlackboardProvider Implementation
    public (StringName Key, object Value)? Provision => (BBDataSig.HitboxComponent, this);
    #endregion

    #region Events
    public event Action<HurtboxComponent2D, IAttackPayload> OnHitRegistered = delegate { };
    public event Action OnAttackStarted = delegate { };
    public event Action OnAttackFinished = delegate { };
    #endregion

    #region Configuration
    [ExportGroup("Hit Behavior")]
    [Export] public bool IsContinuous { get; set; } = false;
    [Export] public float ContinuousTickInterval { get; set; } = 0.1f;
    [Export] public GCol.Array<CombatEffectFactory> DefaultEffects { get; set; } = new();
    [Export] public bool AutoStartWithDefault { get; set; } = false;
    #endregion

    #region Public State
    public bool IsActive { get; private set; }
    public IAttackPayload CurrentPayload { get; private set; }

    /// <summary>
    /// Optional pre-hit hook that filters/modifies the payload before ProcessHit.
    /// Set by game-layer components (e.g., reaction systems) to intercept combat payloads.
    /// Cleared on pool reset for clean reuse. The original payload is always preserved
    /// for OnHitRegistered subscribers regardless of what the interceptor returns.
    /// </summary>
    public IPayloadInterceptor2D? PayloadInterceptor { get; set; }
    #endregion

    #region Private State
    private HurtboxComponent2D? _selfHurtbox = null;
    private readonly HashSet<HurtboxComponent2D> _hitHurtboxes = new();
    private double _tickTimer = 0.0;

    /// <summary>
    /// Number of physics frames to retry the pending overlap check.
    /// Mirrors the 3D broadphase-sync workaround: GetOverlappingAreas() needs
    /// physics to update AFTER Monitoring=true, which can take 2-3 frames when
    /// spawned during physics callbacks.
    /// </summary>
    private const int PendingOverlapRetryFrames = 3;

    private int _pendingOverlapRetries = 0;
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        bb?.TryGet(BBDataSig.HurtboxComponent, out _selfHurtbox);

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Godot Lifecycle
    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;

        // Start cold. Deferred deactivation because _Ready CAN run inside physics callbacks
        // when AddChild is called during OnAreaEntered signal chains.
        Deactivate();

        if (AutoStartWithDefault)
        {
            StartDefaultAttack();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pendingOverlapRetries > 0 && Monitoring)
        {
            _pendingOverlapRetries--;
            ProcessOverlappingAreas();
        }

        if (!IsActive) { return; }
        if (!IsContinuous) { return; }

        _tickTimer += delta;

        if (_tickTimer >= ContinuousTickInterval)
        {
            _tickTimer = 0.0;
            _hitHurtboxes.Clear();
            ProcessOverlappingAreas();
        }
    }
    #endregion

    #region Public API

    public void StartAttack(IAttackPayload payload)
    {
        if (!IsInitialized)
        {
            Shared.JmoLogger.Warning(this, "StartAttack BLOCKED - not initialized!");
            return;
        }

        CurrentPayload = payload;
        _hitHurtboxes.Clear();
        _tickTimer = 0.0;

        OnAttackStarted?.Invoke();

        Shared.JmoLogger.Debug(this, $"StartAttack - pre-Activate Monitoring={Monitoring}");
        Activate();
        Shared.JmoLogger.Debug(this, $"StartAttack - post-Activate Monitoring={Monitoring} (deferred, actual change next frame)");

        _pendingOverlapRetries = PendingOverlapRetryFrames;
    }

    public void EndAttack()
    {
        if (!IsInitialized || !IsActive) { return; }

        CurrentPayload = null;
        _hitHurtboxes.Clear();

        Deactivate();

        OnAttackFinished?.Invoke();
    }

    /// <summary>
    /// Resets hitbox state for pool reuse. Called via IPoolResetable.
    /// CRITICAL: Clears accumulated event handlers — without this, Nth reuse
    /// fires N handlers per hit.
    /// </summary>
    public void OnPoolReset()
    {
        _pendingOverlapRetries = 0;

        OnHitRegistered = delegate { };
        OnAttackStarted = delegate { };
        OnAttackFinished = delegate { };

        // Clear interceptor for clean pool state.
        // Game-layer components (e.g., ReactionComponent) re-wire on each pool cycle.
        PayloadInterceptor = null;

        IsContinuous = false;
        ContinuousTickInterval = 0.1f;

        if (!IsActive) { return; }

        CurrentPayload = null;
        _hitHurtboxes.Clear();
        DeactivateImmediate();
    }

    public void StartDefaultAttack(Node? attacker = null, Node? source = null, IStatProvider? stats = null)
    {
        attacker ??= Owner ?? this;
        source ??= this;

        var payload = new CombatPayload(attacker, source);

        foreach (var factory in DefaultEffects)
        {
            if (factory != null)
            {
                payload.AddEffect(factory.Create(stats));
            }
        }

        StartAttack(payload);
    }
    #endregion

    #region Core Logic

    private void Activate()
    {
        SetDeferred(PropertyName.Monitoring, true);
        SetDeferred(PropertyName.Monitorable, true);
        SetPhysicsProcess(true);
        IsActive = true;
    }

    private void ActivateImmediate()
    {
        Monitoring = true;
        Monitorable = true;
        SetPhysicsProcess(true);
        IsActive = true;
    }

    private void Deactivate()
    {
        IsActive = false;
        SetDeferred(PropertyName.Monitoring, false);
        SetDeferred(PropertyName.Monitorable, false);
        SetPhysicsProcess(false);
    }

    private void DeactivateImmediate()
    {
        IsActive = false;
        Monitoring = false;
        Monitorable = false;
        SetPhysicsProcess(false);
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is HurtboxComponent2D hurtbox)
        {
            TryHitHurtbox(hurtbox);
        }
    }

    private void ProcessOverlappingAreas()
    {
        if (!Monitoring)
        {
            Shared.JmoLogger.Debug(this, "ProcessOverlappingAreas SKIPPED - Monitoring=false");
            return;
        }

        var areas = GetOverlappingAreas();
        Shared.JmoLogger.Debug(this, $"ProcessOverlappingAreas found {areas.Count} overlapping areas");
        foreach (var area in areas)
        {
            if (area is HurtboxComponent2D hurtbox)
            {
                TryHitHurtbox(hurtbox);
            }
        }
    }

    private void TryHitHurtbox(HurtboxComponent2D hurtbox)
    {
        if (!IsActive || CurrentPayload == null)
        {
            Shared.JmoLogger.Debug(this, $"TryHitHurtbox BLOCKED - IsActive={IsActive}, HasPayload={CurrentPayload != null}");
            return;
        }

        if (_selfHurtbox != null && hurtbox == _selfHurtbox)
        {
            Shared.JmoLogger.Debug(this, "TryHitHurtbox BLOCKED - self-hit prevention");
            return;
        }

        if (HasCollisionExceptionWith(hurtbox.Owner))
        {
            Shared.JmoLogger.Debug(this, $"TryHitHurtbox BLOCKED - collision exception with {hurtbox.Owner?.Name}");
            return;
        }

        if (!_hitHurtboxes.Add(hurtbox))
        {
            Shared.JmoLogger.Debug(this, "TryHitHurtbox BLOCKED - already hit this target");
            return;
        }

        // Pre-hit Interception: filter payload via game-layer interceptor (if wired).
        // Original CurrentPayload is preserved for OnHitRegistered observers — the interceptor
        // only affects what ProcessHit sees. See HitboxComponent3D for the defensive-guard
        // rationale (null return / thrown exception both silently drop the hit otherwise).
        IAttackPayload payloadForProcessHit = CurrentPayload;
        if (PayloadInterceptor != null)
        {
            try
            {
                var interceptResult = PayloadInterceptor.InterceptPayload(hurtbox, CurrentPayload);
                if (interceptResult == null)
                {
                    Shared.JmoLogger.Error(this, $"PayloadInterceptor returned null (contract violation) — falling back to original payload");
                }
                else
                {
                    payloadForProcessHit = interceptResult;
                }
            }
            catch (System.Exception ex)
            {
                Shared.JmoLogger.Error(this, $"PayloadInterceptor threw {ex.GetType().Name}: {ex.Message} — falling back to original payload");
            }
        }

        bool wasAccepted = hurtbox.ProcessHit(payloadForProcessHit);

        if (wasAccepted)
        {
            Shared.JmoLogger.Info(this, $"[HIT] HIT ACCEPTED by {hurtbox.Owner?.Name}");
            // Always notify with the ORIGINAL payload — interceptor must not affect observers.
            OnHitRegistered?.Invoke(hurtbox, CurrentPayload);
        }
        else
        {
            Shared.JmoLogger.Info(this, $"[HIT] HIT REJECTED by {hurtbox.Owner?.Name}");
        }
    }

    /// <summary>
    /// Checks if this hitbox's owner has a collision exception with the target.
    /// Bridges Area2D detection with PhysicsBody2D collision exceptions — same
    /// architecture as the 3D variant.
    /// </summary>
    private bool HasCollisionExceptionWith(Node? target)
    {
        if (target == null)
        {
            Shared.JmoLogger.Warning(this, "HasCollisionExceptionWith: target (hurtbox.Owner) is null");
            return false;
        }

        if (Owner is ICombatExceptionProvider exceptionProvider)
        {
            var combatExceptions = exceptionProvider.CombatExceptionIds;
            if (combatExceptions != null && combatExceptions.Contains(target.GetInstanceId()))
            {
                return true;
            }
        }

        if (IsContinuous) { return false; }

        if (Owner is not PhysicsBody2D ownerBody || target is not PhysicsBody2D targetBody)
        {
            return false;
        }

        try
        {
            var exceptions = ownerBody.GetCollisionExceptions();
            return exceptions.Contains(targetBody);
        }
        catch
        {
            Shared.JmoLogger.Warning(this, "GetCollisionExceptions failed (Godot #77793 - freed body in exceptions list)");
            return false;
        }
    }
    #endregion
}
