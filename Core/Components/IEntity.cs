namespace Jmodot.Core.Components;

using System.Collections.Generic;
using AI.BB;

public interface IEntity
{
    IBlackboard BB { get; }
    IReadOnlyList<IComponent> Components { get; }
}
