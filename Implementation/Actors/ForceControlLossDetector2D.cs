namespace Jmodot.Implementation.Actors;

using System;
using Core.Actors;
using Core.Movement;
using Core.Pooling;
using Shared;

/// <summary>
/// 2D twin of <see cref="ForceControlLossDetector"/>. Monitors velocity to detect
/// when external forces have overwhelmed the entity's ability to maintain control.
/// Exposes typed state via properties + events; no blackboard writes.
///
/// Architecture note: reads ALREADY-SCALED velocity (stability handles force reduction
/// inside MovementProcessor2D). No resistance math here — avoids double-dipping.
/// </summary>
[GlobalClass]
public partial class ForceControlLossDetector2D : Node, IPoolResetable
{
    [Export] public float ControlLossThreshold { get; set; } = 15.0f;
    [Export] public float ControlRegainThreshold { get; set; } = 5.0f;
    [Export] public float EvaluationInterval { get; set; } = 0.1f;

    public bool IsControlLost { get; private set; }
    public ForceContext2D? CurrentContext { get; private set; }

    public event Action<ForceContext2D>? ControlLost;
    public event Action? ControlRegained;

    private ICharacterController2D? _controller;
    private ExternalForceReceiver2D? _forceReceiver;
    private Node2D? _owner;

    private float _timeSinceLastEval;

    public void Initialize(
        ICharacterController2D controller,
        ExternalForceReceiver2D forceReceiver,
        Node2D owner)
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
            CurrentContext = new ForceContext2D
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
