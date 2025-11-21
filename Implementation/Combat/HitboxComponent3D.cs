using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;

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
    public partial class HitboxComponent3D : Area3D, IComponent
    {
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
        [ExportGroup("Hit Behavior")]

        /// <summary>
        /// If true, targets remaining in the hitbox will be hit repeatedly.
        /// </summary>
        [Export]
        public bool IsContinuous { get; set; } = false;

        /// <summary>
        /// Seconds between hits in Continuous mode. 0 = Every Physics Frame.
        /// </summary>
        [Export]
        public float ContinuousTickInterval { get; set; } = 0.1f;
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
        #endregion

        #region IComponent Implementation
        public bool IsInitialized { get; private set; }

        public bool Initialize(IBlackboard bb)
        {
            bb.TryGet(BBDataSig.HitboxComp, out _selfHurtbox);

            // Hitbox is generally autonomous, receiving data from its controller.
            IsInitialized = true;
            OnPostInitialize();
            return true;
        }

        public void OnPostInitialize()
        {
            AreaEntered += OnAreaEntered;
        }

        public Node GetUnderlyingNode() => this;
        #endregion

        #region Godot Lifecycle
        public override void _Ready()
        {
            // Start "Cold"
            Monitoring = false;
            Monitorable = false;
            IsActive = false;
            SetPhysicsProcess(false);
        }

        public override void _PhysicsProcess(double delta)
        {
            // Only runs if IsContinuous == true and StartAttack() was called.
            if (!IsActive) { return; }

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
            if (!IsInitialized) { return; }

            CurrentPayload = payload;
            IsActive = true;
            _hitHurtboxes.Clear();
            _tickTimer = 0.0;

            // Activate Physics.
            // SetDeferred ensures we don't crash if called during a physics callback.
            SetDeferred(PropertyName.Monitoring, true);
            SetDeferred(PropertyName.Monitorable, true);

            OnAttackStarted?.Invoke();

            // Attempt to hit anything already inside the volume.
            // Note: If this node was previously Monitoring=False, GetOverlappingAreas
            // might rely on the *next* physics frame to update.
            ProcessOverlappingAreas();

            if (IsContinuous)
            {
                SetPhysicsProcess(true);
            }
        }

        public void EndAttack()
        {
            if (!IsInitialized || !IsActive) return;

            CurrentPayload = null;
            IsActive = false;
            _hitHurtboxes.Clear();

            SetDeferred(PropertyName.Monitoring, false);
            SetDeferred(PropertyName.Monitorable, false);

            if (IsContinuous)
            {
                SetPhysicsProcess(false);
            }

            OnAttackFinished?.Invoke();
        }
        #endregion

        #region Core Logic

        private void OnAreaEntered(Area3D area)
        {
            if (area is HurtboxComponent3D hurtbox)
            {
                TryHitHurtbox(hurtbox);
            }
        }

        private void ProcessOverlappingAreas()
        {
            // Check all currently overlapping areas.
            // Essential for "Spawn on top" or "Beam" logic.
            var areas = GetOverlappingAreas();
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
            if (!IsActive || CurrentPayload == null) { return; }

            // 1. Self-Hit Prevention
            if (hurtbox == _selfHurtbox) { return; }
            //if (CurrentPayload.Attacker != null && hurtbox.Owner == CurrentPayload.Attacker) return;

            // 2. Debounce / Multi-Hit Check
            // If hurtbox is already in the set, we skip it.
            if (!_hitHurtboxes.Add(hurtbox)) return;

            // 3. The Handshake (Direct Method Call)
            bool wasAccepted = hurtbox.ProcessHit(CurrentPayload);

            if (wasAccepted)
            {
                OnHitRegistered?.Invoke(hurtbox, CurrentPayload);
            }
        }
        #endregion
    }
