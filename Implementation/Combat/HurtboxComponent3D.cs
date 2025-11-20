namespace Jmodot.Implementation.Combat;

// HurtboxComponent3D.cs
using Godot;
using System;
using System.Collections.Generic;
using Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Components;

/// <summary>
/// A specialized Area3D representing a damageable region. This component acts as a
/// "Context Provider" for its owner. Its responsibilities are:
/// 1. To perform simple, binary "early-out" checks like invulnerability to save processing.
/// 2. To annotate incoming attack payloads with local, spatial context (e.g., this is a weak point).
/// 3. To forward the annotated payload to a central ICombatTarget for all complex logical processing.
///
/// It does NOT perform complex calculations like blocking or stat-based damage reduction.
/// That responsibility lies with the ICombatTarget and other central systems.
/// </summary>
[GlobalClass]
public partial class HurtboxComponent3D : Area3D, IComponent
{
    #region Events

    /// <summary>
    /// Fired after this hurtbox has received a validated hit and forwarded it to the CombatTarget.
    /// </summary>
    public event Action<IAttackPayload> OnHitForwarded = delegate { };

    #endregion

    #region Exports

    [ExportGroup("Core Configuration")]
    [Export(PropertyHint.NodePathValidTypes, "Node")]
    private NodePath _combatTarget_NodePath;

    /// <summary>
    /// If true, this hurtbox will ignore all incoming hits. A cheap, binary check.
    /// </summary>
    [ExportGroup("Local Context & State")]
    [Export]
    public bool IsInvulnerable { get; set; } = false;

    /// <summary>
    /// Is this hurtbox a special zone, like a weak point or an armored plate?
    /// </summary>
    [Export]
    public bool IsWeakPoint { get; private set; } = false;
    [Export]
    public float WeakPointMultiplier { get; private set; } = 1.5f;

    // We can define a Resource for this to make it more data-driven.
    // [Export] public CombatEffectType WeakPointEffectType { get; private set; }

    #endregion

    #region Public Properties

    public ICombatTarget CombatTarget { get; private set; }

    #endregion

    #region IComponent Implementation

    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        IsInitialized = true;
        return true;
    }

    public void OnPostInitialize()
    {
        if (_combatTarget_NodePath != null && GetNodeOrNull(_combatTarget_NodePath) is ICombatTarget target)
        {
            CombatTarget = target;
        }
        else
        {
            GD.PrintErr($"Hurtbox on '{Owner.Name}' could not find a valid ICombatTarget at path: '{_combatTarget_NodePath}'.");
        }
    }
    public Node GetUnderlyingNode() => this;
    
    #endregion

    #region Godot Lifecycle

    public override void _Ready()
    {
        AreaEntered += OnPotentialHitboxEntered;
    }

    #endregion

    #region Core Logic: The Handshake

    private void OnPotentialHitboxEntered(Area3D area)
    {
        if (area is HitboxComponent3D hitbox)
        {
            hitbox.OnHurtboxDetected += ReceiveValidatedHit;
        }
    }

    private void ReceiveValidatedHit(HurtboxComponent3D detectedHurtbox, IAttackPayload payload)
    {
        if (detectedHurtbox != this) return;

        // Unsubscribe immediately.
        if (detectedHurtbox.GetParent() is HitboxComponent3D hitbox)
        {
            hitbox.OnHurtboxDetected -= ReceiveValidatedHit;
        }

        // --- Gatekeeper & Context Provider Stage ---

        // 1. Perform the cheap, binary "early-out" check.
        if (IsInvulnerable) return;

        // 2. Ensure we have a target to forward to.
        if (CombatTarget == null) return;

        // 3. Annotate the payload with local context if necessary.
        IAttackPayload finalPayload = AnnotatePayload(payload);

        // 4. Forward the final payload to the central brain for logical processing.
        CombatTarget.ProcessPayload(finalPayload);

        // 5. Fire our own event for local feedback.
        OnHitForwarded?.Invoke(finalPayload);
    }

    /// <summary>
    /// Adds contextual information to the payload based on this hurtbox's properties.
    /// </summary>
    private IAttackPayload AnnotatePayload(IAttackPayload originalPayload)
    {
        if (!IsWeakPoint)
        {
            // If there's no special context, return the original payload to avoid allocation.
            return originalPayload;
        }

        // Create a new list of effects, starting with the original ones.
        var annotatedEffects = new List<CombatEffect>(originalPayload.Effects);

        // Add our new contextual effect. The ICombatTarget will now know this was a weak point hit.
        // A more advanced system would use a pre-defined CombatEffectType Resource here.
        // For now, we'll simulate with a placeholder.
        // annotatedEffects.Add(new CombatEffect(WeakPointEffectType, WeakPointMultiplier));

        // Let's create a temporary placeholder until the resource is made
        var weakPointEffectType = new CombatEffectType();
        weakPointEffectType.ResourceName = "WeakPointHit";
        annotatedEffects.Add(new CombatEffect(weakPointEffectType, WeakPointMultiplier));


        return new AnnotatedAttackPayload(originalPayload.Attacker, annotatedEffects);
    }


    #endregion
}
