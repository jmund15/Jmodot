namespace Jmodot.Implementation.AI.HSM;

using Godot;
using Implementation.Combat.Status;
using Jmodot.Core.Combat;
using GColl = Godot.Collections;

/// <summary>
/// Shared, project-authored table of default <see cref="BehaviorAlterationProfile"/>s
/// keyed by the behavior-altering <see cref="CombatTag"/> that triggers them. Assigned to
/// a <see cref="BehaviorSuppressedState"/> via its <c>SharedDefaults</c> export so many
/// entities inherit one tag→profile mapping instead of each authoring its own copy.
/// </summary>
/// <remarks>
/// Resolution order in <see cref="BehaviorSuppressedState"/>: the entity's own per-tag
/// <c>ProfileMap</c> wins first (including an explicit null value, which resolves to that
/// state's <c>DefaultProfile</c>); only when the active tag has no ProfileMap entry does
/// the state consult this shared table. Introducing a new suppressing status for every
/// entity at once is then a single edit here. Godot's Inspector cannot edit
/// <c>Dictionary&lt;Resource, Resource&gt;</c> — author entries directly in .tres/.tscn text.
/// </remarks>
[GlobalClass, Tool]
public partial class SuppressionProfileSet : Resource
{
    /// <summary>
    /// Per-trigger-tag default profile. Consulted by <see cref="BehaviorSuppressedState"/>
    /// when the active tag has no entry in the entity's own ProfileMap.
    /// </summary>
    [Export] public GColl.Dictionary<CombatTag, BehaviorAlterationProfile> Defaults { get; set; } = new();
}
