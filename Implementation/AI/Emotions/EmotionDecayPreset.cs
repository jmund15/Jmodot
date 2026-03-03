namespace Jmodot.Implementation.AI.Emotions;

/// <summary>
///     Preset decay curve shapes for <see cref="EmotionDecayProfile"/>. All presets produce
///     curves in the intuitive 1->0 direction (full intensity at stimulus, zero when fully decayed).
///     What designers see in the Inspector is exactly what happens at runtime — no hidden
///     transformations or inversions.
/// </summary>
public enum EmotionDecayPreset
{
    /// <summary>Straight diagonal 1->0 — uniform linear fade. Default.</summary>
    Linear,

    /// <summary>Fast initial drop, slow lingering tail. Fear from a scare — spikes then echoes.</summary>
    SharpFade,

    /// <summary>Holds steady, then drops off quickly. Building dread that suddenly resolves.</summary>
    LingeringFade,

    /// <summary>Holds at max, cliff-drops to zero. Shield of bravery that shatters.</summary>
    PlateauDrop,

    /// <summary>S-curve: slow start, fast middle, slow end. Natural emotional processing.</summary>
    SmoothFade,

    /// <summary>Flat 1.0 — no decay. Permanent emotion, must be removed explicitly.</summary>
    Permanent,

    /// <summary>Use a manually-assigned custom curve (designer-authored 1->0).</summary>
    Custom
}
