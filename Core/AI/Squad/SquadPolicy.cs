namespace Jmodot.Core.AI.Squad;

using Godot;

/// <summary>
/// Ordered, stateless directive-selection strategy. A <c>SquadDirectiveBrain</c> walks its policy chain
/// and publishes the first non-null result; <c>null</c> = abstain → next policy in the chain.
/// </summary>
/// <remarks>
/// BLAST RADIUS — <see cref="SquadPolicy"/> instances are shared <c>.tres</c> Resources referenced by
/// multiple <c>SquadDirectiveBrain</c> nodes. A mutable instance field silently corrupts state across
/// EVERY squad sharing the instance. All mutable context MUST arrive via the snapshot; keep subclass
/// fields to <c>[Export]</c>-authored configuration only.
/// </remarks>
[GlobalClass, Tool]
public abstract partial class SquadPolicy : Resource
{
    /// <summary>Returns the directive this policy prescribes for <paramref name="snapshot"/>, or <c>null</c> to abstain.</summary>
    public abstract SquadDirectiveDefinition? Evaluate(in SquadSnapshot snapshot);
}
