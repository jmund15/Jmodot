namespace Jmodot.Examples.AI.HSM.TransitionConditions;

using Core.AI.BB;
using Core.AI.HSM;
using Core.Input;
using Implementation.AI.BB;
using Jmodot.Core.Shared.Attributes;

[GlobalClass]
public partial class InputIntentBoolCondition : TransitionCondition
{
    [Export, RequiredExport] private InputAction _requiredAction = null!;
    [Export] private bool _requiredIntent;
    public override bool Check(Node agent, IBlackboard bb)
    {
        var intentSource = bb.Get<IIntentSource>(BBDataSig.IntentSource);
        var intents = intentSource.GetProcessIntents(); // called in process frame so do this one right?

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
