namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Jmodot.Core.AI.HSM;
using Jmodot.Core.Movement;
using Jmodot.Implementation.AI.BB;


[GlobalClass]
public partial class VelocityMagnitudeCondition3D : TransitionCondition
{
    [Export] private float _velocityThreshold;
    [Export] private NumericalConditionType _numericalCondition;
    public override bool Check(Godot.Node agent, IBlackboard bb)
    {
        var charController = bb.Get<ICharacterController3D>(BBDataSig.CharacterController)!;
        var velocity = charController.Velocity;
        return _numericalCondition.CalculateFloatCondition(velocity.Length(), _velocityThreshold);
    }
}
