namespace Jmodot.Implementation.Visual.WorldUi;

/// <summary>How a world-UI prompt draws against the world around it.</summary>
public enum WorldUiDepthMode
{
    /// <summary>Depth-tested with the world: the art and the world occlude the prompt.</summary>
    SortedWithWorld,

    /// <summary>Drawn over the world at <see cref="WorldUiStyle.OnTopRenderPriority"/>, with no depth test.</summary>
    AlwaysOnTop,
}
