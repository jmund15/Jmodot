namespace Jmodot.Core.Visual.Animation.Sprite;

public enum AnimUpdateMode
{
    /// <summary>
    /// Starts the new animation from the beginning (Time = 0).
    /// </summary>
    Reset,

    /// <summary>
    /// Maintains the absolute time position (e.g. 0.5s -> 0.5s).
    /// Useful for direction changes where animations have similar lengths.
    /// </summary>
    MaintainTime,

    /// <summary>
    /// Maintains the relative progress (e.g. 50% -> 50%).
    /// Useful when switching between animations of different lengths (e.g. Walk -> Run).
    /// </summary>
    MaintainPercent
}
