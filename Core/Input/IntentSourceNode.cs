namespace Jmodot.Core.Input;

using System.Collections.Generic;
using Jmodot.Core.Input;

public abstract partial class IntentSourceNode : Node, IIntentSource
{
    public abstract IReadOnlyDictionary<InputAction, IntentData> GetProcessIntents();
    public abstract IReadOnlyDictionary<InputAction, IntentData> GetPhysicsIntents();
    public abstract T GetIntent<T>(InputAction inputAction);

    /// <summary>
    /// Current input profile, exposed for UI prompt systems (C3). Default is
    /// null — subclasses that drive players from a device profile
    /// (<see cref="Jmodot.Implementation.Input.PlayerIntentSource"/>) override
    /// to expose their applied profile.
    /// </summary>
    public virtual InputMappingProfile? CurrentProfile => null;
}
