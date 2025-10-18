namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.HSM;
using Core.Input;
using Implementation.AI.BB;

[GlobalClass]
public partial class InputIntentBoolCondition : TransitionCondition
{
    public IIntentSource IntentSource { get; private set; } = null!;
    [Export] private InputAction _requiredAction = null!;
    [Export] private bool _requiredIntent;
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
        if (!requiredIntent.TryGetBool(out var boolIntent))
        {
            return false;
        }
        return boolIntent!.Value == _requiredIntent;
    }
}
