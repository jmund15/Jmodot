namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;

public class AnimatedStateSimple : IAnimatedState
{
    public bool IsAnimated { get; }
    public StringName? AnimationName { get; }
    public AnimationOrchestrator? AnimationOrchestrator { get; }

    // reusable logic here
}
