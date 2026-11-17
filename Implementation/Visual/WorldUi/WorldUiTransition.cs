namespace Jmodot.Implementation.Visual.WorldUi;

using Godot;

/// <summary>
/// How a <see cref="WorldUiPrompt"/> shows and hides. A transition is a strategy: a new <c>.tres</c> adds a
/// variant with no change to the prompt. Duration 0 means no animation.
/// </summary>
[GlobalClass, Tool]
public abstract partial class WorldUiTransition : Resource
{
}
