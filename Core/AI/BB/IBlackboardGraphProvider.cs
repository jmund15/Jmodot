namespace Jmodot.Core.AI.BB;

/// <summary>
/// Resolver — given an owning context (typically a Node), return the associated graph.
/// Distinct from <see cref="IBlackboardProvider"/>: that interface is a capability-marker for
/// auto-registration into a blackboard (push, KV property); this one is a pull-style lookup (method).
/// The shared <c>*Provider</c> suffix is coincidental — they are not a subclass family.
/// </summary>
public interface IBlackboardGraphProvider
{
    IBlackboardGraph? GetGraph();
}
