namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// Marks a node whose whole subtree visual effects must not touch: a node that implements it owns the
/// colour and material of everything below it. <see cref="VisualNodeAggregator.CollectSprites(Godot.Node)"/>
/// stops at it, so effect controllers, material binders and tints that walk an actor's sprites skip it.
/// </summary>
public interface IVisualEffectExempt
{
}
