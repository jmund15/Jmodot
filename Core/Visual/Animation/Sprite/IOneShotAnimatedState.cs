namespace Jmodot.Core.Visual.Animation.Sprite;

using Implementation.AI.HSM;

// TODO: is this even useful?
public interface IOneShotAnimatedState : IAnimatedState
{
    State PostAnimationState { get; }
}
