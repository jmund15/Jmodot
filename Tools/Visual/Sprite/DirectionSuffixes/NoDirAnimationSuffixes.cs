namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;

[GlobalClass, Tool]
public sealed partial class NoDirAnimationSuffixes : AnimationDirectionSuffixes
{
    public NoDirAnimationSuffixes()
    {
        this.DirectionSuffixes = this.NoDirSuffixes;
    }
    private List<string> NoDirSuffixes { get; init; } = new()
    {
        ""
    };
}
