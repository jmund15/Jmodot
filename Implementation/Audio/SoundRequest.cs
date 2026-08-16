namespace Jmodot.Implementation.Audio;

using Godot;

/// <summary>
/// A per-request play order submitted to <see cref="IAudioDirector.Play"/>. Carries the
/// <see cref="SoundProfile"/> to play and, for positional profiles, the world position of the
/// source. All presentation (volume/bus/priority/spatial mode) lives on the immutable profile;
/// this struct carries only per-request state, so a single authored profile can drive many
/// concurrent plays without shared mutable state.
/// </summary>
public readonly record struct SoundRequest(SoundProfile Profile, Vector3 Position);
