namespace Jmodot.Implementation.Audio;

using Godot;

/// <summary>
/// The one funnel every sound in the game plays through. Trigger surfaces (the
/// <see cref="SoundEffectComponent"/>, spell and UI code) submit <see cref="SoundRequest"/>s;
/// the implementing director owns the voice pool, stealing policy, bus routing, and listener
/// override.
/// </summary>
public interface IAudioDirector
{
    /// <summary>Plays a request through the voice pool, dropping it gracefully when no voice is available.</summary>
    void Play(SoundRequest request);

    /// <summary>Stops every active voice and returns all slots to their free lists (run-end / scene transition).</summary>
    void StopAll();

    /// <summary>Overrides the hearing origin for cutscenes; auto-clears when the node dies.</summary>
    void SetListenerOverride(Node3D? listener);

    /// <summary>Restores the camera-default hearing origin.</summary>
    void ClearListenerOverride();
}
