namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

/// <summary>
/// A composite task that executes its children in order until one succeeds. It succeeds
/// as soon as one of its children succeeds. It fails only if all of its children fail.
/// Also known as a "Fallback" node.
/// <para>
/// Memoizes the running child — once a child returns Running, it is not preempted by
/// higher-priority siblings becoming eligible later. For preemption semantics, use
/// <see cref="ReactiveSelector"/>.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class Selector : PrioritySelectorBase
{
}
