namespace Jmodot.Implementation.Actors;

using Core.Actors;
using Core.Movement;
using AI.BB;
using Core.AI.BB;
using Shared;

/// <summary>
/// Monitors the entity's velocity to detect when external forces have overwhelmed
/// the entity's ability to maintain control. Writes ControlLost flag and ForceContext
/// to the blackboard for HSM state transitions.
///
/// Architecture note: This detector reads ALREADY-SCALED velocity (Phase 5 stability
/// handles force reduction). No resistance math here — avoids double-dipping.
/// </summary>
[GlobalClass]
public partial class ForceControlLossDetector : Node
{
    /// <summary>Velocity magnitude at which control is lost.</summary>
    [Export] public float ControlLossThreshold { get; set; } = 15.0f;

    /// <summary>Velocity magnitude below which control is regained (hysteresis).</summary>
    [Export] public float ControlRegainThreshold { get; set; } = 5.0f;

    /// <summary>How often to evaluate (seconds). Not every frame for performance.</summary>
    [Export] public float EvaluationInterval { get; set; } = 0.1f;

    private ICharacterController3D? _controller;
    private ExternalForceReceiver3D? _forceReceiver;
    private IBlackboard? _bb;
    private Node3D? _owner;

    private bool _isControlLost;
    private float _timeSinceLastEval;

    public void Initialize(
        ICharacterController3D controller,
        ExternalForceReceiver3D forceReceiver,
        IBlackboard bb,
        Node3D owner)
    {
        _controller = controller;
        _forceReceiver = forceReceiver;
        _bb = bb;
        _owner = owner;
        _isControlLost = false;
        _timeSinceLastEval = 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_controller == null || _forceReceiver == null || _bb == null || _owner == null)
        {
            return;
        }

        _timeSinceLastEval += (float)delta;
        if (_timeSinceLastEval < EvaluationInterval)
        {
            return;
        }

        _timeSinceLastEval = 0f;

        var velocity = _controller.Velocity;
        var velocityMagnitude = velocity.Length();

        var shouldBeLost = ControlLossEvaluator.Evaluate(
            velocityMagnitude,
            _isControlLost,
            ControlLossThreshold,
            ControlRegainThreshold);

        if (shouldBeLost == _isControlLost)
        {
            return;
        }

        _isControlLost = shouldBeLost;

        if (_isControlLost)
        {
            var (dominantSource, dominantForce) = _forceReceiver.GetDominantForceSource(_owner);
            var context = new ForceContext
            {
                DominantForceDirection = dominantForce.Normalized(),
                ForceMagnitude = dominantForce.Length(),
                DominantSource = dominantSource
            };

            _bb.Set(BBDataSig.ControlLost, true);
            _bb.Set(BBDataSig.ForceContext, context);

            JmoLogger.Info(this,
                $"Control LOST — velocity={velocityMagnitude:F1}, source={dominantSource?.Name ?? "unknown"}");
        }
        else
        {
            _bb.Set(BBDataSig.ControlLost, false);

            JmoLogger.Info(this,
                $"Control REGAINED — velocity={velocityMagnitude:F1}");
        }
    }
}
