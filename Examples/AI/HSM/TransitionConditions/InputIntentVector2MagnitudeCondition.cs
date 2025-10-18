namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.HSM;
using Core.Input;
using Implementation.AI.BB;

[GlobalClass]
public partial class InputIntentVector2MagnitudeCondition : TransitionCondition
{
    public IIntentSource IntentSource { get; private set; } = null!;
    [Export] private InputAction _requiredAction = null!;
    [Export] private float _requiredIntentMagnitude;
    [Export] private NumericalConditionType _numericalCondition;
    protected override void OnInit()
    {
        base.OnInit();
        IntentSource = BB.Get<IIntentSource>(BBDataSig.IntentSource);
    }

    public override bool Check()
    {
        var intents = IntentSource.GetProcessIntents(); // called in process frame so do this one right?

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
