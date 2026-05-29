namespace Jmodot.Core.Modifiers;

using Stats;

/// <summary>
///     The generic interface for any object that can modify a value. It contains the
///     priority contract; the fold behaviour itself is data-driven via a typed StageRule on
///     the concrete modifier interfaces (<see cref="IFloatModifier" />, <see cref="IIntModifier" />,
///     etc.). Cancel/context-gate tag filtering is an opt-in capability — see
///     <see cref="ITaggableModifier" />.
/// </summary>
/// <typeparam name="T">The type of value to be modified.</typeparam>
public interface IModifier<T>
{
    /// <summary>The priority of this modifier within its stage. Higher numeric values are applied first.</summary>
    int Priority { get; }
}
