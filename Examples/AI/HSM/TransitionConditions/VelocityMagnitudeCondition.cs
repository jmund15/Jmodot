namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Jmodot.Core.AI.HSM;
using Jmodot.Core.Movement;
using Jmodot.Implementation.AI.BB;

public enum NumericalConditionType
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo,
}
[GlobalClass]
public partial class VelocityMagnitudeCondition : TransitionCondition
{
    private ICharacterController2D _charController = null!;
    [Export] private float _velocityThreshold;
    [Export] private NumericalConditionType _numericalCondition;
    protected override void OnInit()
    {
        _charController = BB.GetVar<ICharacterController2D>(BBDataSig.CharacterController)!;
    }

    public override bool Check()
    {
        var velocity = _charController.Velocity;
        switch (_numericalCondition)
        {
            case NumericalConditionType.GreaterThan:
                if (velocity.Length() > _velocityThreshold)
                {
                    return true;
                }

                return false;
            case NumericalConditionType.LessThan:
                if (velocity.Length() < _velocityThreshold)
                {
                    return true;
                }
                return false;
            case NumericalConditionType.EqualTo:
                if (Mathf.IsEqualApprox(velocity.Length(), _velocityThreshold))
                {
                    return true;
                }
                return false;
            case NumericalConditionType.NotEqualTo:
                if (!Mathf.IsEqualApprox(velocity.Length(), _velocityThreshold))
                {
                    return true;
                }
                return false;
            default:
                return false;
        }
    }
}
