namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Jmodot.Core.AI.HSM;
using Jmodot.Core.Movement;
using Jmodot.Implementation.AI.BB;


[GlobalClass]
public partial class VelocityMagnitudeCondition3D : TransitionCondition
{
    private ICharacterController3D _charController = null!;
    [Export] private float _velocityThreshold;
    [Export] private NumericalConditionType _numericalCondition;
    protected override void OnInit()
    {
        _charController = BB.Get<ICharacterController3D>(BBDataSig.CharacterController)!;
    }

    public override bool Check()
    {
        var velocity = _charController.Velocity;
        return _numericalCondition.CalculateFloatCondition(velocity.Length(), _velocityThreshold);
    }
}
