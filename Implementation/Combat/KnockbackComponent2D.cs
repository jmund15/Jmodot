using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Actors;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Implementation.Actors;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

namespace Jmodot.Implementation.Combat;

/// <summary>
/// 2D twin of <see cref="KnockbackComponent3D"/>. Handles knockback application
/// for CharacterBody2D entities. Subscribes to CombatantComponent.CombatResultEvent
/// and applies impulses immediately via MovementProcessor2D when DamageResult
/// contains force.
///
/// REQUIRED BLACKBOARD DEPENDENCIES:
/// - BBDataSig.MovementProcessor (IMovementProcessor2D) — applies the impulse
/// - BBDataSig.CombatantComponent (CombatantComponent) — source of combat events
///
/// Note: the 3D variant's FlattenKnockback export is omitted — top-down 2D has
/// no "up axis" to flatten against. 2D consumers that want cardinal-axis
/// clamping should do it in their own layer.
///
/// Registers on the shared BBDataSig.KnockbackComponent key.
/// </summary>
[GlobalClass]
public partial class KnockbackComponent2D : Node2D, IComponent, IBlackboardProvider
{
    #region IBlackboardProvider Implementation
    public (StringName Key, object Value)? Provision => (BBDataSig.KnockbackComponent, this);
    #endregion

    #region SIGNALS

    /// <summary>
    /// Emitted when knockback is applied. Useful for VFX, audio, or other reactive systems.
    /// </summary>
    [Signal] public delegate void KnockbackAppliedEventHandler(Vector2 direction, float force);

    #endregion

    #region DEPENDENCIES

    private IMovementProcessor2D _movementProcessor = null!;
    private CombatantComponent _combatant = null!;

    #endregion

    #region COMPONENT_VARIABLES

    /// <summary>
    /// Resistance to knockback forces. Uses diminishing returns formula:
    /// resistanceFactor = 1 / (1 + stability).
    /// 0 = no resistance (full knockback), 1 = half force, 3 = quarter force.
    /// </summary>
    [Export(PropertyHint.Range, "0,20,0.1")] public float Stability { get; set; }

    #endregion

    #region COMPONENT_UPDATES

    public override void _Ready()
    {
        base._Ready();
        ProcessMode = ProcessModeEnum.Disabled;
    }

    #endregion

    #region COMPONENT_LOGIC

    private void OnCombatResult(CombatResult result)
    {
        if (result is DamageResult damageResult && damageResult.Force > 0)
        {
            // DamageResult.Direction is Vector3; 2D flattens via XZ→XY projection
            // (HurtboxComponent2D adapts with the same convention).
            var direction2D = new Vector2(damageResult.Direction.X, damageResult.Direction.Z);
            ApplyKnockback(direction2D, damageResult.Force);
        }
    }

    /// <summary>
    /// Applies a knockback impulse to the MovementProcessor2D.
    /// </summary>
    /// <param name="direction">The normalized direction of the knockback.</param>
    /// <param name="force">The magnitude of the knockback force.</param>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        var scaledForce = force * StabilityScaling.CalculateResistanceFactor(Stability);
        var impulse = direction * scaledForce;

        _movementProcessor.ApplyImpulse(impulse);
        EmitSignal(SignalName.KnockbackApplied, direction, scaledForce);
    }

    public override void _ExitTree()
    {
        if (_combatant != null)
        {
            _combatant.CombatResultEvent -= OnCombatResult;
        }
    }

    #endregion

    #region INTERFACE_IMPLEMENTATION

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public bool Initialize(IBlackboard bb)
    {
        if (!bb.TryGet(BBDataSig.MovementProcessor, out _movementProcessor!) || _movementProcessor == null)
        {
            JmoLogger.Error(this, "Required dependency BBDataSig.MovementProcessor not found");
            return false;
        }

        if (!bb.TryGet(BBDataSig.CombatantComponent, out _combatant!) || _combatant == null)
        {
            JmoLogger.Error(this, "Required dependency BBDataSig.CombatantComponent not found");
            return false;
        }

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;
        _combatant.CombatResultEvent += OnCombatResult;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region CONFIGURATION_WARNINGS

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
    }

    #endregion
}
