namespace Jmodot.Implementation.AI.Emotions;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.AI.Affinities;
using Jmodot.Core.AI.Emotions;

/// <summary>
///     Maps emotion types to personality-driven amplification curves. When a critter
///     receives a raw stimulus (e.g., fear = 0.5), this profile amplifies it based on
///     the critter's personality: <c>amplified = raw × curve(affinityValue)</c>.
///
///     A timid critter (fear affinity = 0.9) with a steep curve amplifies fear strongly.
///     A brave critter (fear affinity = 0.2) with the same curve barely feels it.
///
///     Designed as a shared resource — one "Cowardly" profile can be assigned to many critters.
/// </summary>
[Tool]
[GlobalClass]
public partial class EmotionAmplificationProfile : Resource
{
    [Export] private Godot.Collections.Dictionary<EmotionType, AmplificationEntry> _entries = new();

    /// <summary>
    /// Calculates the amplified intensity for a given emotion, looking up the entry
    /// and delegating to the static pure function. Returns raw intensity if no entry exists.
    /// </summary>
    /// <param name="emotion">The emotion type to amplify.</param>
    /// <param name="rawIntensity">Raw stimulus intensity before personality.</param>
    /// <param name="affinityValue">Current affinity value [0,1] from AIAffinitiesComponent.</param>
    public float GetAmplifiedIntensity(EmotionType emotion, float rawIntensity, float affinityValue)
    {
        if (!_entries.TryGetValue(emotion, out var entry))
        {
            return rawIntensity;
        }

        return CalculateAmplifiedIntensity(
            rawIntensity, affinityValue,
            entry.AmplificationCurve, entry.DefaultMultiplier);
    }

    /// <summary>
    /// Returns the affinity linked to a given emotion, or null if no entry exists.
    /// Used by AIEmotionalStateComponent to know which affinity to read.
    /// </summary>
    public Affinity? GetLinkedAffinity(EmotionType emotion)
    {
        return _entries.TryGetValue(emotion, out var entry) ? entry.LinkedAffinity : null;
    }

    /// <summary>
    /// Pure static amplification calculation. Extracted for testability.
    /// <c>result = Clamp(rawIntensity × multiplier, 0, 1)</c>
    /// where multiplier = curve.Sample(affinityValue) or defaultMultiplier if no curve.
    /// </summary>
    /// <param name="rawIntensity">Raw stimulus intensity.</param>
    /// <param name="affinityValue">Personality trait value [0,1].</param>
    /// <param name="curve">Optional amplification curve. Null = use defaultMultiplier.</param>
    /// <param name="defaultMultiplier">Fallback when curve is null. Default 1.0.</param>
    public static float CalculateAmplifiedIntensity(
        float rawIntensity, float affinityValue,
        Curve? curve, float defaultMultiplier = 1f)
    {
        float multiplier = curve?.Sample(affinityValue) ?? defaultMultiplier;
        return Mathf.Clamp(rawIntensity * multiplier, 0f, 1f);
    }

    #region Test Helpers

    internal void SetEntry(EmotionType emotion, AmplificationEntry entry)
    {
        _entries[emotion] = entry;
    }

    #endregion
}
