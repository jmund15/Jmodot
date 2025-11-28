namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;

// TODO: make into 'strategy' or POCO that can be plugged into any state easily
public class AnimatedStateSimple : IAnimatedState
{
    public bool IsAnimated { get; }
    public StringName? AnimationName { get; }
    public AnimationOrchestrator? AnimationOrchestrator { get; }

    // reusable logic here
}
