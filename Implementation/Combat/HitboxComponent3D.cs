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
    /// A generic collision sensor that delivers attack payloads to Hurtboxes.
    ///
    /// Responsibilities:
    /// - Detect Overlaps (AreaEntered).
    /// - Debounce (Prevent multi-hits on the same frame/attack cycle).
    /// - Deliver Payload (Call Hurtbox.ProcessHit).
    ///
    /// Modes:
    /// - Standard: Hits target once per StartAttack().
    /// - Continuous: Hits targets repeatedly based on TickInterval.
    /// </summary>
    [GlobalClass]
    public partial class HitboxComponent3D : Area3D, IComponent, IBlackboardProvider, IPoolResetable
    {
        #region IBlackboardProvider Implementation
        /// <summary>
        /// Auto-registers this component with the blackboard during EntityNodeComponentsInitializer.
        /// </summary>
        public (StringName Key, object Value)? Provision => (BBDataSig.HitboxComponent, this);
        #endregion

        #region Events
        /// <summary>
        /// Fired when a hit is successfully validated and accepted by a hurtbox.
        /// Useful for spawning Hit VFX (Sparks, Blood) at the hitbox location.
        /// </summary>
        public event Action<HurtboxComponent3D, IAttackPayload> OnHitRegistered = delegate { };

        public event Action OnAttackStarted = delegate { };
        public event Action OnAttackFinished = delegate { };
        #endregion

        #region Configuration
        /// <summary>
        /// If true, targets remaining in the hitbox will be hit repeatedly.
        /// </summary>
        [ExportGroup("Hit Behavior")]
        [Export] public bool IsContinuous { get; set; } = false;

        /// <summary>
        /// Seconds between hits in Continuous mode. 0 = Every Physics Frame.
        /// </summary>
        [Export] public float ContinuousTickInterval { get; set; } = 0.1f;

        [Export] public GCol.Array<CombatEffectFactory> DefaultEffects { get; set; } = new();

        /// <summary>
        /// If true, the hitbox will automatically start an attack using its DefaultEffects on Ready.
        /// </summary>
        [Export] public bool AutoStartWithDefault { get; set; } = false;
        #endregion

        #region Public State
        public bool IsActive { get; private set; }
        public IAttackPayload CurrentPayload { get; private set; }
        #endregion

        #region Private State

        private HurtboxComponent3D? _selfHurtbox = null;
        // Tracks targets hit during the current session/tick to prevent duplicates.
        private readonly HashSet<HurtboxComponent3D> _hitHurtboxes = new();
        private double _tickTimer = 0.0;


        /// <summary>
        /// Number of physics frames to retry the pending overlap check.
        /// GetOverlappingAreas() needs the physics broadphase to update AFTER Monitoring=true,
        /// which can take 2-3 frames when spawned during physics callbacks (SetDeferred timing).
        /// Without retries, fast-moving SpawnEffect children miss targets they spawn inside of.
        /// </summary>
        private const int PendingOverlapRetryFrames = 3;
        #endregion

        #region IComponent Implementation
        public bool IsInitialized { get; private set; }

        public bool Initialize(IBlackboard bb)
        {
            bb?.TryGet(BBDataSig.HurtboxComponent, out _selfHurtbox);

            // Hitbox is generally autonomous, receiving data from its controller.
            IsInitialized = true;
            Initialized();
            OnPostInitialize();
            return true;
        }

        public void OnPostInitialize()
        {
            // Signal connection moved to _Ready() - only needs to happen once per node lifetime
            // OnPostInitialize() is called every Initialize(), which happens on each pool reuse
        }

        public event Action Initialized = delegate { };

        public Node GetUnderlyingNode() => this;
        #endregion

        #region Godot Lifecycle
        public override void _Ready()
        {
            // Connect signal once - _Ready() only runs once per node lifetime (even with pooling)
            AreaEntered += OnAreaEntered;

            // Start "Cold" - use DEFERRED deactivation because _Ready() CAN run inside a physics
            // callback when AddChild() is called during OnAreaEntered signal chains
            // (e.g. SpawnOneShotSpellEffect.Spawn() during spell destruction).
            // Safe: IsActive=false is synchronous and guards TryHitHurtbox from phantom hits.
            Deactivate();

            if (AutoStartWithDefault)
            {
                StartDefaultAttack();
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            // CRITICAL: Check for pending overlap scan FIRST (before early returns).
            // This catches targets that were already overlapping when the hitbox activated.
            // GetOverlappingAreas() needs the physics broadphase to sync after Monitoring=true.
            //
            // IMPORTANT: We must wait until Monitoring is ACTUALLY true before processing.
            // When spawned during physics callbacks (like SpawnEffect OnDestroy), the deferred
            // SetDeferred(Monitoring, true) may not have executed yet. Keep checking each frame.
            //
            // RETRY LOGIC: The broadphase can take 2-3 frames to register new overlaps after
            // Monitoring is enabled. We retry for PendingOverlapRetryFrames to catch overlaps
            // that the first check misses. Once a hit is registered, retries stop naturally
            // via the _hitHurtboxes debounce set (no double-hits).
            if (_pendingOverlapRetries > 0 && Monitoring)
            {
                _pendingOverlapRetries--;
                ProcessOverlappingAreas();
            }

            // Only continue if IsContinuous == true and StartAttack() was called.
            if (!IsActive) { return; }
            if (!IsContinuous) { return; }

            _tickTimer += delta;

            if (_tickTimer >= ContinuousTickInterval)
            {
                _tickTimer = 0.0;

                // Reset tracking to allow re-hitting targets inside the volume.
                _hitHurtboxes.Clear();

                // Manually scan for targets currently inside.
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

            // Activate FIRST so that Monitoring=true is queued before ProcessOverlappingAreas.
            // Deferred calls execute in FIFO order.
            Shared.JmoLogger.Debug(this, $"StartAttack - pre-Activate Monitoring={Monitoring}");
            Activate();
            Shared.JmoLogger.Debug(this, $"StartAttack - post-Activate Monitoring={Monitoring} (deferred, actual change next frame)");

            // CRITICAL FIX: GetOverlappingAreas() requires the physics broadphase to update
            // AFTER Monitoring=true. When spawned during physics callbacks (SpawnEffect OnDestroy),
            // SetDeferred(Monitoring, true) executes at end-of-frame, but the broadphase doesn't
            // update until the NEXT physics tick. Fast-moving projectiles can leave the overlap zone
            // before the broadphase registers it. Retry for a few frames to catch the overlap.
            _pendingOverlapRetries = PendingOverlapRetryFrames;
        }

        /// <summary>
        /// Remaining physics frames to retry the overlap check.
        /// 0 = no pending check. Decremented each physics frame until an overlap is found or retries exhaust.
        /// </summary>
        private int _pendingOverlapRetries = 0;

        public void EndAttack()
        {
            if (!IsInitialized || !IsActive) { return; }

            CurrentPayload = null;
            _hitHurtboxes.Clear();

            Deactivate();

            OnAttackFinished?.Invoke();
        }

        /// <summary>
        /// Resets hitbox state for pool reuse. Called automatically via IPoolResetable.
        /// Uses immediate (synchronous) deactivation since pool reset is NOT a physics callback.
        /// </summary>
        public void OnPoolReset()
        {
            _pendingOverlapRetries = 0;  // Clear pending scan for clean pool state

            // CRITICAL: Clear accumulated event handlers BEFORE the early return.
            // WireCombatListener subscribes OnHitRegistered += each pool cycle.
            // Without clearing, Nth reuse fires N handlers per hit → N Destroy calls.
            OnHitRegistered = delegate { };
            OnAttackStarted = delegate { };
            OnAttackFinished = delegate { };

            // Reset continuous mode to export defaults for clean pool state.
            // WavePullEffectInstance re-sets these during OnInitialize each cast.
            IsContinuous = false;
            ContinuousTickInterval = 0.1f;

            if (!IsActive) { return; }

            // Use immediate deactivation - OnPoolReset is called from pool management, not physics
            CurrentPayload = null;
            _hitHurtboxes.Clear();
            DeactivateImmediate();
        }

        /// <summary>
        /// Creates a payload from the DefaultEffects list and starts an attack.
        /// </summary>
        /// <param name="attacker">The node responsible for the attack (defaults to Owner).</param>
        /// <param name="source">The specific object representing the attack (defaults to this Hitbox or Owner).</param>
        /// <param name="stats">The stat provider used to scale effect values (optional).</param>
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

        /// <summary>
        /// Activates the hitbox using deferred property changes.
        /// REQUIRED when called from physics callbacks (_PhysicsProcess, AreaEntered, BodyEntered).
        /// </summary>
        /// <remarks>
        /// SetDeferred ensures we don't crash by modifying physics state during physics evaluation.
        /// The actual Monitoring/Monitorable changes happen at end of frame.
        /// </remarks>
        private void Activate()
        {
            SetDeferred(PropertyName.Monitoring, true);
            SetDeferred(PropertyName.Monitorable, true);
            SetPhysicsProcess(true);
            IsActive = true;
        }

        /// <summary>
        /// Activates the hitbox immediately (synchronous).
        /// Safe to use in: _Ready(), _Process(), ActivateFromPool(), Initialize().
        /// NOT safe in physics callbacks - use Activate() instead.
        /// </summary>
        private void ActivateImmediate()
        {
            Monitoring = true;
            Monitorable = true;
            SetPhysicsProcess(true);
            IsActive = true;
        }

        /// <summary>
        /// Deactivates the hitbox using deferred property changes.
        /// REQUIRED when called from physics callbacks.
        /// </summary>
        private void Deactivate()
        {
            IsActive = false;
            SetDeferred(PropertyName.Monitoring, false);
            SetDeferred(PropertyName.Monitorable, false);
            SetPhysicsProcess(false);
        }

        /// <summary>
        /// Deactivates the hitbox immediately (synchronous).
        /// Safe to use in: _Ready(), _Process(), ActivateFromPool(), Initialize().
        /// NOT safe in physics callbacks - use Deactivate() instead.
        /// </summary>
        private void DeactivateImmediate()
        {
            IsActive = false;
            Monitoring = false;
            Monitorable = false;
            SetPhysicsProcess(false);
        }

        private void OnAreaEntered(Area3D area)
        {
            if (area is HurtboxComponent3D hurtbox)
            {
                TryHitHurtbox(hurtbox);
            }
        }

        private void ProcessOverlappingAreas()
        {
            // Guard: GetOverlappingAreas() requires Monitoring to be enabled.
            if (!Monitoring)
            {
                Shared.JmoLogger.Debug(this, $"ProcessOverlappingAreas SKIPPED - Monitoring=false");
                return;
            }

            // Check all currently overlapping areas.
            // Essential for "Spawn on top" or "Beam" logic.
            var areas = GetOverlappingAreas();
            Shared.JmoLogger.Debug(this, $"ProcessOverlappingAreas found {areas.Count} overlapping areas");
            foreach (var area in areas)
            {
                if (area is HurtboxComponent3D hurtbox)
                {
                    TryHitHurtbox(hurtbox);
                }
            }
        }

        private void TryHitHurtbox(HurtboxComponent3D hurtbox)
        {
            //Shared.JmoLogger.Debug(this, $"TryHitHurtbox: target={hurtbox.Owner?.Name ?? "NULL"}, IsActive={IsActive}, HasPayload={CurrentPayload != null}");

            if (!IsActive || CurrentPayload == null)
            {
                Shared.JmoLogger.Debug(this, $"TryHitHurtbox BLOCKED - IsActive={IsActive}, HasPayload={CurrentPayload != null}");
                return;
            }

            // 1. Self-Hit Prevention
            if (_selfHurtbox != null && hurtbox == _selfHurtbox)
            {
                Shared.JmoLogger.Debug(this, "TryHitHurtbox BLOCKED - self-hit prevention");
                return;
            }

            // 1.5 Collision Exception Synchronization
            // Area3D detection is independent of PhysicsBody3D collision exceptions.
            // This synchronizes Area3D combat with PhysicsBody3D exceptions (used for sibling spells).
            // The ATTACKER decides "I won't hit this target" - matches how collision exceptions work.
            if (HasCollisionExceptionWith(hurtbox.Owner))
            {
                Shared.JmoLogger.Debug(this, $"TryHitHurtbox BLOCKED - collision exception with {hurtbox.Owner?.Name}");
                return;
            }

            // 2. Debounce / Multi-Hit Check
            // If hurtbox is already in the set, we skip it.
            if (!_hitHurtboxes.Add(hurtbox))
            {
                Shared.JmoLogger.Debug(this, "TryHitHurtbox BLOCKED - already hit this target");
                return;
            }

            // 3. The Handshake (Direct Method Call)
            //Shared.JmoLogger.Debug(this, $"TryHitHurtbox PROCESSING HIT on {hurtbox.Owner?.Name}");
            bool wasAccepted = hurtbox.ProcessHit(CurrentPayload);

            if (wasAccepted)
            {
                Shared.JmoLogger.Debug(this, $"TryHitHurtbox HIT ACCEPTED by {hurtbox.Owner?.Name}");
                OnHitRegistered?.Invoke(hurtbox, CurrentPayload);
            }
            else
            {
                Shared.JmoLogger.Debug(this, $"TryHitHurtbox HIT REJECTED by {hurtbox.Owner?.Name}");
            }
        }

        /// <summary>
        /// Checks if this hitbox's owner has a collision exception with the target.
        /// Returns true if the hit should be skipped.
        /// </summary>
        /// <remarks>
        /// This synchronizes Area3D (hitbox/hurtbox) detection with collision exceptions.
        /// AddCollisionExceptionWith() only affects physics (MoveAndSlide), not Area3D signals.
        /// This method bridges that gap for combat purposes.
        ///
        /// Checks two sources:
        /// 1. ICombatExceptionProvider.CombatExceptionIds - explicit combat-level exceptions (sibling spells)
        /// 2. PhysicsBody3D.GetCollisionExceptions() - physics-level exceptions (fallback)
        /// </remarks>
        private bool HasCollisionExceptionWith(Node? target)
        {
            // Guard: If target (hurtbox.Owner) is null, we can't check exceptions.
            // This can happen for programmatically-created nodes where Owner isn't set.
            if (target == null)
            {
                Shared.JmoLogger.Warning(this, "HasCollisionExceptionWith: target (hurtbox.Owner) is null");
                return false;
            }

            // 1. Check explicit combat exceptions first (sibling spells) - ALWAYS checked
            // ICombatExceptionProvider allows any owner to provide combat-level exceptions
            // without depending on physics exceptions which only work for PhysicsBody3D.
            if (Owner is ICombatExceptionProvider exceptionProvider)
            {
                var combatExceptions = exceptionProvider.CombatExceptionIds;
                if (combatExceptions != null && combatExceptions.Contains(target.GetInstanceId()))
                {
                    return true;
                }
            }

            // 2. Skip physics exception check in continuous mode.
            // Physics exceptions are added by PiercePhysicsStrategy for MoveAndSlide pass-through.
            // In continuous mode, the hitbox must re-hit targets each tick for sustained damage.
            if (IsContinuous) { return false; }

            // 3. Check physics collision exceptions (non-continuous only)
            // Both owner and target must be PhysicsBody3D for collision exceptions to apply
            if (Owner is not PhysicsBody3D ownerBody || target is not PhysicsBody3D targetBody)
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
                // GetCollisionExceptions() can throw if a body in the list was freed (Godot bug #77793)
                return false;
            }
        }
        #endregion
    }
