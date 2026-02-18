namespace Jmodot.Core.Visual.Animation.Sprite;

using Jmodot.Core.AI.HSM;

// TODO: is this even useful?
public interface IOneShotAnimatedState : IAnimatedState
{
    IState PostAnimationState { get; }
}
