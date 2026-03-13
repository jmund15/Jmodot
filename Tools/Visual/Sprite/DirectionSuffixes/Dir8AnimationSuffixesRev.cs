namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;

[GlobalClass, Tool]
public sealed partial class Dir8AnimationSuffixesRev : AnimationDirectionSuffixes
{
    public Dir8AnimationSuffixesRev()
    {
        this.DirectionSuffixes = this.Dir8Suffixes;
    }
    private List<string> Dir8Suffixes { get; init; } = new()
    {
        "left",
        "upLeft",
        "up",
        "upRight",
        "right",
        "downRight",
        "down",
        "downLeft"
    };
}
