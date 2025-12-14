using Godot;
using System;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;

namespace Jmodot.Implementation.Combat;

using Implementation.AI.BB;

/// <summary>
/// Represents the damageable zone of an entity.
/// Acts as a Gateway/Filter to the ICombatant found in the Blackboard.
/// </summary>
[GlobalClass]
public partial class HurtboxComponent3D : Area3D, IComponent
{
    #region Events
    /// <summary>
    /// Fired when a hit is successfully accepted and forwarded to the Combatant.
    /// </summary>
    public event Action<IAttackPayload, HitContext> OnHitReceived = delegate { };
    #endregion

    #region Configuration

    /// <summary>
    /// If true, this entity ignores all hits cheaply (Gatekeeper).
    /// </summary>
    [ExportGroup("State")]
    [Export] public bool IsInvulnerable { get; set; } = false;
    #endregion

    #region State
    private ICombatant _combatant;
    #endregion

    #region IComponent Implementation
    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        // Retrieve the Combatant dependency directly from the Blackboard.
        // This ensures decoupling; the Hurtbox doesn't care *how* the Combatant is implemented.
        if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant))
        {
            // Fail initialization if the Brain (Combatant) is missing.
            return false;
        }

        IsInitialized = true;
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }
    public event Action? Initialized;

    public Node GetUnderlyingNode() => this;
    #endregion

    #region Godot Lifecycle
    public override void _Ready()
    {
        // Passive detection. We don't scan for others (Monitoring=False),
        // but we allow others to scan us (Monitorable=True).
        Monitoring = false;
        Monitorable = true;
    }
    #endregion

    #region Public API

    /// <summary>
    /// Called DIRECTLY by HitboxComponent3D.
    /// This is the receiving end of the Handshake.
    /// </summary>
    /// <returns>True if the hit was processed, False if rejected.</returns>
    public bool ProcessHit(IAttackPayload payload)
    {
        // 1. Validation
        if (!IsInitialized || _combatant == null) { return false; }

        // 2. Gatekeeping (Cheap check)
        if (IsInvulnerable) { return false; }

        // 3. Context Creation
        // Maps the Payload Source to the Context so effects know what hit them.
        HitContext context = new HitContext
        {
            Attacker = payload.Attacker,
            Source = payload.Source
        };

        // 4. Forward to Brain
        // The Combatant executes the logic defined in the Effects.
        _combatant.ProcessPayload(payload, context);

        // 5. Feedback
        OnHitReceived?.Invoke(payload, context);
        return true;
    }
    #endregion
}
