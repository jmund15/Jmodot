namespace Jmodot.Core.AI.Squad;

using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Typed narrowing of <see cref="Category"/> so an <c>[Export] SquadDirectiveDefinition</c> field cannot
/// be assigned an arbitrary world category. Carries the inherited identity data + parent-category
/// hierarchy; hierarchical matching (<see cref="Category.IsOrDescendsFrom"/>) lets a consumer target a
/// directive family or an exact leaf.
/// </summary>
/// <remarks>
/// The inherited <see cref="Category.PerceptionDecay"/> is INERT for directives — do not populate it.
/// Directive instances are consumed ONLY via the <c>BBDataSig.SquadDirective</c> blackboard channel and
/// are NEVER added to an entity's <c>Identity.Categories</c> list.
/// </remarks>
[GlobalClass, Tool]
public partial class SquadDirectiveDefinition : Category
{
}
