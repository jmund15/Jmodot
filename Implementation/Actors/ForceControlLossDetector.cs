namespace Jmodot.Implementation.Actors;

using System;
using Core.Actors;
using Core.Movement;
using Core.Pooling;
using Shared;

/// <summary>
/// Monitors the entity's velocity to detect when external forces have overwhelmed
/// the entity's ability to maintain control. Exposes typed state via properties +
/// events so consumers (HSM states, BT actions, scripted overrides) can subscribe
/// without going through stringly-typed blackboard flags.
///
/// Architecture note: reads ALREADY-SCALED velocity (Phase 5 stability handles force
/// reduction). No resistance math here — avoids double-dipping.
/// </summary>
[GlobalClass]
public partial class ForceControlLossDetector : Node, IPoolResetable
{
    [Export] public float ControlLossThreshold { get; set; } = 15.0f;
    [Export] public float ControlRegainThreshold { get; set; } = 5.0f;
    [Export] public float EvaluationInterval { get; set; } = 0.1f;

    public bool IsControlLost { get; private set; }
    public ForceContext? CurrentContext { get; private set; }

    public event Action<ForceContext>? ControlLost;
    public event Action? ControlRegained;

    private ICharacterController3D? _controller;
    private ExternalForceReceiver3D? _forceReceiver;
    private Node3D? _owner;

    private float _timeSinceLastEval;

    public void Initialize(
        ICharacterController3D controller,
        ExternalForceReceiver3D forceReceiver,
        Node3D owner)
    {
        _controller = controller;
        _forceReceiver = forceReceiver;
        _owner = owner;
        IsControlLost = false;
        CurrentContext = null;
        _timeSinceLastEval = 0f;
    }

    public void OnPoolReset()
    {
        IsControlLost = false;
        CurrentContext = null;
        _timeSinceLastEval = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_controller == null || _forceReceiver == null || _owner == null)
        {
            return;
        }

        _timeSinceLastEval += (float)delta;
        if (_timeSinceLastEval < EvaluationInterval)
        {
            return;
        }

        _timeSinceLastEval = 0f;

        var velocityMagnitude = _controller.Velocity.Length();

        var shouldBeLost = ControlLossEvaluator.Evaluate(
            velocityMagnitude,
            IsControlLost,
            ControlLossThreshold,
            ControlRegainThreshold);

        if (shouldBeLost == IsControlLost)
        {
            return;
        }

        IsControlLost = shouldBeLost;

        if (IsControlLost)
        {
            var (dominantSource, dominantForce) = _forceReceiver.GetDominantForceSource(_owner);
            CurrentContext = new ForceContext
            {
                DominantForceDirection = dominantForce.Normalized(),
                ForceMagnitude = dominantForce.Length(),
                DominantSource = dominantSource,
            };

            JmoLogger.Info(this,
                $"Control LOST — velocity={velocityMagnitude:F1}, source={dominantSource?.Name ?? "unknown"}");

            ControlLost?.Invoke(CurrentContext);
        }
        else
        {
            CurrentContext = null;

            JmoLogger.Info(this,
                $"Control REGAINED — velocity={velocityMagnitude:F1}");

            ControlRegained?.Invoke();
        }
    }
}
