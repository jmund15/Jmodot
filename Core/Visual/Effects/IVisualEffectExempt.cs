namespace Jmodot.Core.Visual.Effects;

/// <summary>
/// Marks a node whose whole subtree visual effects must not touch: a node that implements it owns the
/// colour of everything below it.
/// </summary>
public interface IVisualEffectExempt
{
}
