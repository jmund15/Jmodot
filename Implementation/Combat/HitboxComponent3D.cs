using System;
using System.Collections.Generic;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Stats;
using GCol = Godot.Collections;

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
        #endregion

        #region IComponent Implementation
        public bool IsInitialized { get; private set; }

        public bool Initialize(IBlackboard bb)
        {
            bb?.TryGet(BBDataSig.HurtboxComponent, out _selfHurtbox);

            // Hitbox is generally autonomous, receiving data from its controller.
            IsInitialized = true;
            Initialized?.Invoke();
            OnPostInitialize();
            return true;
        }

        public void OnPostInitialize()
        {
            AreaEntered += OnAreaEntered;
        }

        public event Action? Initialized;

        public Node GetUnderlyingNode() => this;
        #endregion

        #region Godot Lifecycle
        public override void _Ready()
        {
            // Start "Cold"
            Deactivate();

            if (AutoStartWithDefault)
            {
                StartDefaultAttack();
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            // Only runs if IsContinuous == true and StartAttack() was called.
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
            if (!IsInitialized) { return; }

            CurrentPayload = payload;
            _hitHurtboxes.Clear();
            _tickTimer = 0.0;

            OnAttackStarted?.Invoke();

            // Activate FIRST so that Monitoring=true is queued before ProcessOverlappingAreas.
            // Deferred calls execute in FIFO order.
            Activate();

            // Attempt to hit anything already inside the volume.
            // Note: If this node was previously Monitoring=False, GetOverlappingAreas
            // might rely on the *next* physics frame to update.
            // We defer this call to ensure that the 'Monitoring' property update (which is also deferred)
            // has been processed by the engine before we try to query overlaps.
            Callable.From(ProcessOverlappingAreas).CallDeferred();

            //GD.Print($"Hitbox '{Name} Starting Attack!");
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

        private void Activate()
        {
            // SetDeferred ensures we don't crash if called during a physics callback.
            SetDeferred(PropertyName.Monitoring, true);
            SetDeferred(PropertyName.Monitorable, true);
            SetPhysicsProcess(true);
            IsActive = true;
        }

        private void Deactivate()
        {
            IsActive = false;
            // SetDeferred ensures we don't crash if called during a physics callback.
            SetDeferred(PropertyName.Monitoring, false);
            SetDeferred(PropertyName.Monitorable, false);
            SetPhysicsProcess(false);
        }

        private void OnAreaEntered(Area3D area)
        {
            GD.Print($"Area {area.Name} entered Hitbox {Name}");
            if (area is HurtboxComponent3D hurtbox)
            {
                TryHitHurtbox(hurtbox);
            }
        }

        private void ProcessOverlappingAreas()
        {
            // Guard: GetOverlappingAreas() requires Monitoring to be enabled.
            if (!Monitoring) { return; }

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
            if (_selfHurtbox != null &&
                hurtbox == _selfHurtbox) { return; }
            //if (CurrentPayload.Attacker != null && hurtbox.Owner == CurrentPayload.Attacker) return;

            // 2. Debounce / Multi-Hit Check
            // If hurtbox is already in the set, we skip it.
            if (!_hitHurtboxes.Add(hurtbox)) { return; }

            // 3. The Handshake (Direct Method Call)
            bool wasAccepted = hurtbox.ProcessHit(CurrentPayload);

            if (wasAccepted)
            {
                OnHitRegistered?.Invoke(hurtbox, CurrentPayload);
            }
        }
        #endregion
    }
