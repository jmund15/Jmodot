namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Input;
using Implementation.AI.BB;
using Jmodot.Core.Shared.Attributes;

[GlobalClass]
public partial class InputIntentVector2MagnitudeCondition : TransitionCondition
{
    [Export, RequiredExport] private InputAction _requiredAction = null!;
    [Export] private float _requiredIntentMagnitude;
    [Export] private NumericalConditionType _numericalCondition;

    public override bool Check(Node agent, IBlackboard bb)
    {
        var intentSource = bb.Get<IIntentSource>(BBDataSig.IntentSource);
        var intents = intentSource.GetProcessIntents(); // called in process frame so do this one right?

        if (!intents.ContainsKey(_requiredAction))
        {
            return false;
        }
        var requiredIntent = intents[_requiredAction];
        if (!requiredIntent.TryGetVector2(out var vectorIntent))
        {
            return false;
        }
        return _numericalCondition.CalculateFloatCondition(vectorIntent!.Value.Length(), _requiredIntentMagnitude);
    }
}
