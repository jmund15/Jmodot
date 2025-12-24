namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;

[GlobalClass, Tool]
public sealed partial class Dir4AnimationSuffixes : AnimationDirectionSuffixes
{
    public Dir4AnimationSuffixes()
    {
        this.DirectionSuffixes = this.Dir4Suffixes;
    }
    private List<string> Dir4Suffixes { get; init; } = new()
    {
        "up",
        "right",
        "down",
        "left"
    };
}
