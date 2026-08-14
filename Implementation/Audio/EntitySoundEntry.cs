namespace Jmodot.Implementation.Audio;

using Godot;

/// <summary>
/// Whether an animation's sound plays once (<see cref="OneShot"/>) or re-issues at
/// <see cref="EntitySoundEntry.CadenceInterval"/> while the animation keeps running
/// (<see cref="Cadence"/> — footsteps, looped hums).
/// </summary>
public enum PlayMode
{
    /// <summary>Plays the sound a single time on animation start.</summary>
    OneShot = 0,

    /// <summary>Re-issues the sound every <c>CadenceInterval</c> seconds until the animation stops or is superseded.</summary>
    Cadence = 1,
}

/// <summary>
/// One per-animation row of an <see cref="EntitySoundProfile"/>: the animation name matched
/// against the orchestrator's <c>AnimStarted</c> events, the sound it plays, and how it plays.
/// Unknown animations are silently ignored (documented design rule).
/// </summary>
[GlobalClass, Tool]
public partial class EntitySoundEntry : Resource
{
    /// <summary>Animation name matched against the orchestrator's AnimStarted events.</summary>
    [Export] public StringName Animation { get; set; }

    /// <summary>Sound played for this animation. A null Sound drops the request at play (graceful, one-time warning).</summary>
    [Export] public SoundProfile? Sound { get; set; }

    /// <summary>OneShot plays once; Cadence re-issues while the animation runs.</summary>
    [Export] public PlayMode PlayMode { get; set; } = PlayMode.OneShot;

    /// <summary>Seconds between Cadence re-issues. Only read in Cadence mode.</summary>
    [Export] public float CadenceInterval { get; set; } = 0.5f;
}
