namespace Jmodot.Implementation.Audio;

using Godot;

/// <summary>
/// Stealing tier for a sound when the voice pool is exhausted. A lower tier is a
/// better steal candidate; <see cref="Important"/> outranks <see cref="Normal"/>.
/// </summary>
public enum SoundPriority
{
    /// <summary>Default tier. Evictable by any <see cref="Important"/> request.</summary>
    Normal = 0,

    /// <summary>High-impact tier (spell bursts). Never evicted by a <see cref="Normal"/> request.</summary>
    Important = 1,
}

/// <summary>
/// Whether a sound spatializes from a world position (<see cref="Positional"/>) or
/// ignores position entirely (<see cref="Plain"/> — UI and global feedback).
/// </summary>
public enum SpatialMode
{
    /// <summary>Played on a positional voice at the request's world position. Default.</summary>
    Positional = 0,

    /// <summary>Played on a plain voice; unaffected by listener position.</summary>
    Plain = 1,
}

/// <summary>
/// Authored presentation for one sound: the stream variation pool plus the fixed
/// volume, bus, priority, and spatial-mode it plays through. Consumed by the
/// <see cref="IAudioDirector"/> when it plays a <see cref="SoundRequest"/>. Immutable
/// authored config — per-request state lives on the request, never here.
/// </summary>
[GlobalClass, Tool]
public partial class SoundProfile : Resource
{
    /// <summary>Stream variation pool. The engine picks a random stream with its authored pitch/volume range on each play. Null drops the request at play (graceful).</summary>
    [Export] public AudioStreamRandomizer? Streams { get; set; }

    /// <summary>Fixed base volume in dB applied on top of the randomizer's per-play volume range.</summary>
    [Export(PropertyHint.Range, "-60, 12, 0.1")] public float VolumeDb { get; set; }

    /// <summary>Target bus. Unresolvable names fall back to Master with a one-time warning at play.</summary>
    [Export] public StringName Bus { get; set; } = "SFX";

    /// <summary>Stealing tier used by the voice allocator when the pool is exhausted.</summary>
    [Export] public SoundPriority Priority { get; set; } = SoundPriority.Normal;

    /// <summary>Positional spatializes from the request's world position; Plain ignores position (UI/global).</summary>
    [Export] public SpatialMode SpatialMode { get; set; } = SpatialMode.Positional;
}
