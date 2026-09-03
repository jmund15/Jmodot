namespace Jmodot.Implementation.Components;

using Godot;
using AI.BB;
using Core.AI.BB;
using Core.Components;
using Core.Pooling;
using Core.Shared.Attributes;
using Shared;
using StatAttribute = Core.Stats.Attribute;

/// <summary>
/// Writes a stat-driven Mass onto a RigidBody2D and keeps it live across modifiers — the
/// stat-to-physics-property bridge rung of the family <see cref="EntitySizeController"/>
/// establishes. Any RigidBody2D entity with a stat provider on its blackboard can carry this.
/// 3D twin: <see cref="RigidBodyMassController3D"/>.
///
/// CONFIGURATION:
/// - MassAttribute: REQUIRED - must be set in editor (no default fallback)
///
/// BLACKBOARD REQUIREMENTS:
/// - BBDataSig.Stats (required): StatController containing the mass attribute
/// - BBDataSig.Agent (required): the RigidBody2D whose Mass this controller writes
/// </summary>
[Tool, GlobalClass]
public partial class RigidBodyMassController2D : Node, IComponent, IPoolResetable
{
    private const float EngineDefaultMass = 1f;

    [Export, RequiredExport]
    public StatAttribute MassAttribute { get; set; } = null!;

    private Core.Stats.StatController? _stats;
    private RigidBody2D? _body;
    private bool _warnedInvalidMass;

    public bool IsInitialized { get; private set; }
    public event System.Action Initialized = delegate { };

    public override void _Ready()
    {
        base._Ready();

        // [Tool] is carried for _GetConfigurationWarnings alone; the editor must not run the
        // runtime export lint on a scene an author is still assembling.
        if (Engine.IsEditorHint())
        {
            return;
        }

        this.ValidateRequiredExports();
    }

    public bool Initialize(IBlackboard bb)
    {
        if (MassAttribute == null)
        {
            JmoLogger.Error(this, "RigidBodyMassController2D: MassAttribute is required but not configured!");
            return false;
        }

        if (!bb.TryGet(BBDataSig.Stats, out _stats) || _stats == null)
        {
            JmoLogger.Error(this, "RigidBodyMassController2D requires StatController in Blackboard!");
            return false;
        }

        bb.TryGet(BBDataSig.Agent, out Node agent);
        if (agent is not RigidBody2D body)
        {
            JmoLogger.Error(this,
                $"RigidBodyMassController2D requires a RigidBody2D at BBDataSig.Agent, got " +
                $"{(agent == null ? "null" : agent.GetType().Name)}!");
            return false;
        }

        _body = body;
        _stats.Subscribe(MassAttribute, OnMassChanged);

        var currentMass = _stats.GetStatValue<float>(MassAttribute, EngineDefaultMass);
        WriteMass(currentMass);

        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;

    private void OnMassChanged(Variant newValue)
    {
        if (newValue.VariantType == Variant.Type.Float)
        {
            WriteMass(newValue.AsSingle());
        }
    }

    // Guards the engine's own division against a stat resolving to zero or non-finite — an
    // entity with an incomplete sheet should still move, not degenerate the body's physics.
    private void WriteMass(float mass)
    {
        if (!(mass > 0f) || !float.IsFinite(mass))
        {
            if (!_warnedInvalidMass)
            {
                JmoLogger.Warning(this,
                    $"RigidBodyMassController2D resolved a non-positive or non-finite Mass ({mass}) — " +
                    "leaving the engine default in place.");
                _warnedInvalidMass = true;
            }

            return;
        }

        if (_body != null)
        {
            _body.Mass = mass;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var parent = GetParent();
        if (parent is RigidBody2D)
        {
            return System.Array.Empty<string>();
        }

        return new[]
        {
            $"RigidBodyMassController2D's parent must be a RigidBody2D, got " +
            $"{(parent == null ? "null" : parent.GetType().Name)}.",
        };
    }

    /// <summary>
    /// Resets state for pool reuse. Unsubscribes from stat changes to prevent subscription leaks
    /// and restores the engine default Mass — its one home outside the component that restores it.
    /// </summary>
    public void OnPoolReset()
    {
        if (_stats != null && MassAttribute != null)
        {
            _stats.Unsubscribe(MassAttribute, OnMassChanged);
        }

        if (_body != null && GodotObject.IsInstanceValid(_body))
        {
            _body.Mass = EngineDefaultMass;
        }

        _stats = null;
        _body = null;
        _warnedInvalidMass = false;
        IsInitialized = false;
    }
}
