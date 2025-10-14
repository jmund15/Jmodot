namespace Jmodot.Core.Input;

using System.Collections.Generic;
using Jmodot.Core.Input;

public abstract partial class IntentSourceNode : Node, IIntentSource
{
    public abstract IReadOnlyDictionary<InputAction, IntentData> GetProcessIntents();
    public abstract IReadOnlyDictionary<InputAction, IntentData> GetPhysicsIntents();
}
