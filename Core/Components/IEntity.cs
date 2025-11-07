namespace Jmodot.Core.Components;

using System.Collections.Generic;
using AI.BB;

public interface IEntity
{
    IBlackboard BB { get; }
    List<IComponent> Components { get; }
}
