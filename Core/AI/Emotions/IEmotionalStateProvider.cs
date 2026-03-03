namespace Jmodot.Core.AI.Emotions;

using System;
using System.Collections.Generic;

/// <summary>
///     Read-only interface for consumers (HSM conditions, Utility AI, Steering) to query
///     the current emotional state. The component stores itself in the BB via
///     <c>BBDataSig.EmotionalState</c>, and consumers cast it to this interface.
/// </summary>
public interface IEmotionalStateProvider
{
    /// <summary>Returns the current intensity for the given emotion, or null if not active.</summary>
    float? GetIntensity(EmotionType type);

    /// <summary>Tries to get the current intensity. Returns false if the emotion is not active.</summary>
    bool TryGetIntensity(EmotionType type, out float intensity);

    /// <summary>Returns all currently active emotions with their intensities.</summary>
    IEnumerable<(EmotionType Type, float Intensity)> GetAllActiveEmotions();

    /// <summary>
    /// Fired when an emotion's intensity changes meaningfully (stimulus, decay tick, or removal).
    /// Parameters: (EmotionType, oldIntensity, newIntensity). newIntensity = 0 means removed.
    /// </summary>
    event Action<EmotionType, float, float> EmotionChanged;
}
