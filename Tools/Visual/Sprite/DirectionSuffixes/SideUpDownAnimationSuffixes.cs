namespace Jmodot.Tools.Visual.Sprite;

/// <summary>
/// Defines side, down, and up animation rows for sprites mirrored across the horizontal axis.
/// </summary>
[GlobalClass, Tool]
public sealed partial class SideUpDownAnimationSuffixes : AnimationDirectionSuffixes
{
    /// <summary>
    /// Initializes the side, down, and up suffixes in sheet-row order.
    /// </summary>
    public SideUpDownAnimationSuffixes()
    {
        this.DirectionSuffixes = new() { "", "down", "up" };
    }
}
