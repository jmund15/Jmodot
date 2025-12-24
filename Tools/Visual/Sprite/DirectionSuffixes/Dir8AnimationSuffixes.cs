namespace Jmodot.Tools.Visual.Sprite;

using System.Collections.Generic;

[GlobalClass, Tool]
public sealed partial class Dir8AnimationSuffixes : AnimationDirectionSuffixes
{
    public Dir8AnimationSuffixes()
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
