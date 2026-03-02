namespace Jmodot.Core.AI.Emotions;

using System;
using Jmodot.Implementation.AI.Emotions;

/// <summary>
///     Stateful runtime data for a single active emotion. Analogous to
///     <c>Perception3DInfo</c> but with delta-accumulated time tracking instead of
///     wall-clock timestamps. The decay math is computed on-demand when
///     <see cref="CurrentIntensity"/> is queried — the only per-frame cost is
///     <see cref="Tick"/> accumulating elapsed time (one float addition).
/// </summary>
public class EmotionInstance
{
    /// <summary>The type of emotion this instance represents.</summary>
    public EmotionType Type { get; }

    /// <summary>Intensity at last stimulus (after personality amplification).</summary>
    public float BaseIntensity { get; private set; }

    /// <summary>Seconds since last stimulus, accumulated via delta in _Process.</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>Decay profile controlling the shape and duration of intensity falloff.</summary>
    public EmotionDecayProfile DecayProfile { get; private set; }

    /// <summary>
    /// Current effective intensity, computed on-demand from base + elapsed + profile.
    /// No per-frame evaluation cost — only computed when read.
    /// </summary>
    public float CurrentIntensity => DecayProfile.CalculateIntensity(BaseIntensity, ElapsedTime);

    /// <summary>
    /// Whether this emotion is still meaningfully active.
    /// Threshold of 0.001 prevents floating-point noise from keeping emotions "alive."
    /// </summary>
    public bool IsActive => CurrentIntensity > 0.001f;

    public EmotionInstance(EmotionType type, float baseIntensity, EmotionDecayProfile decayProfile)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        BaseIntensity = baseIntensity;
        ElapsedTime = 0f;
        DecayProfile = decayProfile ?? throw new ArgumentNullException(nameof(decayProfile));
    }

    /// <summary>
    /// Advances elapsed time by the frame delta. Called by AIEmotionalStateComponent._Process.
    /// </summary>
    public void Tick(float delta)
    {
        if (delta > 0f)
        {
            ElapsedTime += delta;
        }
    }

    /// <summary>
    /// Refreshes the emotion from a new stimulus. Takes the max of current and new intensity
    /// (D4: stimulus stacking) and resets the decay timer.
    /// </summary>
    public void Refresh(float newIntensity, EmotionDecayProfile profile)
    {
        BaseIntensity = Math.Max(CurrentIntensity, newIntensity);
        ElapsedTime = 0f;
        DecayProfile = profile ?? DecayProfile;
    }
}
