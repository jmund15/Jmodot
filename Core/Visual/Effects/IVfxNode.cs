namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// Capability marker: a node carrying it renders a visual effect (flash, burst, smoke, debris) rather than
/// a world object. Presentation rules that bind world sprites exempt a sprite at or under such a node.
/// Queried with <c>is</c>; a node type opts in by implementing it and carries no members for it.
/// </summary>
public interface IVfxNode
{
}
