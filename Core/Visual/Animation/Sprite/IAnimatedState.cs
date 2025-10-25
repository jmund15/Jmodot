namespace Jmodot.Core.Visual.Animation.Sprite;

using Implementation.Visual.Animation.Sprite;

public interface IAnimatedState
{
    bool IsAnimated { get; }
    StringName? AnimationName { get; }
    AnimationOrchestrator? AnimationOrchestrator { get; }
}
