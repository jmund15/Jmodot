namespace Jmodot.Core.Visual.Animation.Sprite;

// TODO: refactor, why are we asking 'IsAnimated', its an animated state!!
public interface IAnimatedState
{
    bool IsAnimated { get; }
    StringName? AnimationName { get; }
    IAnimationOrchestrator? AnimationOrchestrator { get; }
}
