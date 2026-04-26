namespace Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// How a slot's animator participates in composite timing.
/// </summary>
public enum AnimationSyncMode
{
    /// <summary>Slot's animator drives the composite's master timing. Exactly one Master per composite.</summary>
    Master,

    /// <summary>Slot's animator is registered with the composite and synced to the master's normalized time.</summary>
    Slave,

    /// <summary>Slot's animator is NOT registered with the composite. Plays its own animations independently.</summary>
    Independent,
}
