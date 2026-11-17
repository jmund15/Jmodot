namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// Capability marker: a node carrying it renders a visual effect rather than a world object: effect energy such as a
/// cast-charge glow, a flash or burst, a spark or smoke. Matter that becomes a lit object (a held projectile, debris)
/// is not an effect. Presentation rules that bind world sprites exempt a sprite at or under such a node.
/// Queried with <c>is</c>; a node type opts in by implementing it and carries no members for it.
/// </summary>
public interface IVfxNode
{
}
