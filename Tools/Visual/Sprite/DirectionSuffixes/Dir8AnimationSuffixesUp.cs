namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;

[GlobalClass, Tool]
public sealed partial class Dir8AnimationSuffixesUp : AnimationDirectionSuffixes
{
    public Dir8AnimationSuffixesUp()
    {
        this.DirectionSuffixes = this.Dir8Suffixes;
    }
    private List<string> Dir8Suffixes { get; init; } = new()
    {
        "up",
        "upRight",
        "right",
        "downRight",
        "down",
        "downLeft",
        "left",
        "upLeft"
    };
}
