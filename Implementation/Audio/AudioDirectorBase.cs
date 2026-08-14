namespace Jmodot.Implementation.Audio;

using System;
using Godot;
using Jmodot.Core.Audio;
using Jmodot.Core.Pooling;
using Jmodot.Implementation.Shared;

/// <summary>
/// Read seam for the audio volume settings. The Part-2 project settings source implements and
/// raises it; the director subscribes at <see cref="AudioDirectorBase.Init"/> and applies the
/// mapped bus volumes same-frame (the apply-immediate rule).
/// </summary>
public interface ISettingsReadSource
{
    /// <summary>Reads the current value of a setting key.</summary>
    T GetSetting<T>(StringName key);

    /// <summary>Raised when any setting changes. Change-only — never fires at disk hydration.</summary>
    event Action SettingsChanged;
}

/// <summary>
/// One-funnel audio director. Owns the long-lived voice pool (free-list allocation plus
/// oldest-lowest-tier stealing via <see cref="VoiceAllocator"/>), play/stop, the camera-default
/// listener with a reparent-safe <see cref="Node3D"/> override, and the settings-key→bus volume
/// utilities. Consuming projects subclass this and forward the concrete instance into
/// <see cref="AudioSeam"/> at startup.
/// </summary>
/// <remarks>
/// The director is instantiated directly in Logic tests via <see cref="Init"/> (no scene tree);
/// the voice nodes are created when the node enters the tree, so off-tree <c>new()</c> instances
/// carry no playback surface. Buses are <see cref="AudioServer"/> singletons available without a
/// tree, which is what lets the bus tests run off-tree.
/// </remarks>
public partial class AudioDirectorBase : Node, IAudioDirector
{
    private const int PositionalVoiceCount = 24;
    private const int PlainVoiceCount = 8;

    private readonly VoiceAllocator _positionalAllocator = new(PositionalVoiceCount);
    private readonly VoiceAllocator _plainAllocator = new(PlainVoiceCount);
    private readonly VoiceChannel[] _positionalVoices = new VoiceChannel[PositionalVoiceCount];
    private readonly VoiceChannel[] _plainVoices = new VoiceChannel[PlainVoiceCount];

    private ISettingsReadSource? _settings;
    private Node3D? _listenerOverride;
    private bool _warnedNullStreams;
    private bool _warnedUnresolvableBus;
    private bool _warnedUnresolvableApplyBus;

    /// <summary>
    /// Maps an audio volume setting key to its target bus. Returns null for any key that is not
    /// one of the three audio volume keys, so <see cref="ApplyVolumeSetting"/> ignores unrelated
    /// setting changes.
    /// </summary>
    public static StringName? BusForVolumeKey(StringName key)
    {
        if (key == "audio/master_volume") { return "Master"; }
        if (key == "audio/music_volume") { return "Music"; }
        if (key == "audio/sfx_volume") { return "SFX"; }
        return null;
    }

    /// <summary>
    /// Subscribes to the settings source and applies current values to the mapped buses. Safe to
    /// call on a fresh, off-tree instance (the harness seam for Logic tests).
    /// </summary>
    public void Init(ISettingsReadSource source)
    {
        if (_settings != null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
        }
        _settings = source;
        if (_settings == null)
        {
            return;
        }
        _settings.SettingsChanged += OnSettingsChanged;
        ApplyAllVolumeSettings();
    }

    /// <summary>Applies one setting key's value to its mapped bus (no-op for non-audio keys or unresolvable buses).</summary>
    public void ApplyVolumeSetting(StringName key)
    {
        var bus = BusForVolumeKey(key);
        if (bus == null || _settings == null)
        {
            return;
        }
        int index = AudioServer.GetBusIndex(bus);
        if (index == -1)
        {
            WarnOnce(ref _warnedUnresolvableApplyBus, $"Audio bus '{bus}' does not exist; volume setting not applied.");
            return;
        }
        AudioServer.SetBusVolumeLinear(index, _settings.GetSetting<float>(key));
    }

    /// <summary>Stops every active voice and returns all slots to their free lists.</summary>
    public void StopAll()
    {
        foreach (var voice in _positionalVoices)
        {
            voice?.StopAllStreams();
        }
        foreach (var voice in _plainVoices)
        {
            voice?.StopAllStreams();
        }
        for (int i = 0; i < PositionalVoiceCount; i++)
        {
            _positionalAllocator.Release(new VoiceHandle(i));
        }
        for (int i = 0; i < PlainVoiceCount; i++)
        {
            _plainAllocator.Release(new VoiceHandle(i));
        }
    }

    /// <summary>Overrides the hearing origin for cutscenes; auto-clears when the node dies.</summary>
    public void SetListenerOverride(Node3D? listener) => _listenerOverride = listener;

    /// <summary>Restores the camera-default hearing origin.</summary>
    public void ClearListenerOverride() => _listenerOverride = null;

    public void Play(SoundRequest request)
    {
        var profile = request.Profile;
        if (profile == null || profile.Streams == null)
        {
            WarnOnce(ref _warnedNullStreams, "SoundProfile.Streams is null; request dropped.");
            return;
        }
        if (AudioServer.GetBusIndex(profile.Bus) == -1)
        {
            WarnOnce(ref _warnedUnresolvableBus, $"Audio bus '{profile.Bus}' does not exist; falling back to Master.");
        }

        bool positional = profile.SpatialMode == SpatialMode.Positional;
        var handle = positional
            ? _positionalAllocator.Allocate(profile.Priority)
            : _plainAllocator.Allocate(profile.Priority);
        if (handle.IsNone)
        {
            return;
        }
        var voice = positional ? _positionalVoices[handle.SlotIndex] : _plainVoices[handle.SlotIndex];
        voice?.Play(profile, request.Position);
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        for (int i = 0; i < PositionalVoiceCount; i++)
        {
            var node = new AudioStreamPlayer3D { MaxPolyphony = 4 };
            AddChild(node);
            _positionalVoices[i] = new VoiceChannel(node, null);
        }
        for (int i = 0; i < PlainVoiceCount; i++)
        {
            var node = new AudioStreamPlayer();
            AddChild(node);
            _plainVoices[i] = new VoiceChannel(null, node);
        }
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateListenerOverride();
        ReleaseFinishedVoices();
    }

    private void ApplyAllVolumeSettings()
    {
        ApplyVolumeSetting("audio/master_volume");
        ApplyVolumeSetting("audio/music_volume");
        ApplyVolumeSetting("audio/sfx_volume");
    }

    private void OnSettingsChanged() => ApplyAllVolumeSettings();

    private void UpdateListenerOverride()
    {
        if (_listenerOverride == null)
        {
            return;
        }
        // Reparent-safe auto-clear: never a TreeExiting-based clear (fires on reparent too).
        if (!GodotObject.IsInstanceValid(_listenerOverride) || _listenerOverride.IsQueuedForDeletion())
        {
            _listenerOverride = null;
        }
    }

    private void ReleaseFinishedVoices()
    {
        for (int i = 0; i < PositionalVoiceCount; i++)
        {
            var voice = _positionalVoices[i];
            if (voice != null && voice.IsBusy && !voice.IsStreamActive())
            {
                _positionalAllocator.Release(new VoiceHandle(i));
                voice.IsBusy = false;
            }
        }
        for (int i = 0; i < PlainVoiceCount; i++)
        {
            var voice = _plainVoices[i];
            if (voice != null && voice.IsBusy && !voice.IsStreamActive())
            {
                _plainAllocator.Release(new VoiceHandle(i));
                voice.IsBusy = false;
            }
        }
    }

    private void WarnOnce(ref bool fired, string message)
    {
        if (fired)
        {
            return;
        }
        fired = true;
        JmoLogger.Warning(this, message);
    }

    /// <summary>
    /// The thin node adapter that maps a <see cref="VoiceHandle"/> back to a live voice node and
    /// feeds stream-finish state back to the director. Each voice runs an
    /// <see cref="AudioStreamPolyphonic"/> with polyphony 4 (the authoritative per-voice gate);
    /// a voice returns to the free list when its stream ends.
    /// </summary>
    private sealed class VoiceChannel : IPoolResetable
    {
        private readonly AudioStreamPlayer3D? _positional;
        private readonly AudioStreamPlayer? _plain;
        private AudioStreamPlaybackPolyphonic? _playback;
        private int _activeStreamId = -1;

        public VoiceChannel(AudioStreamPlayer3D? positional, AudioStreamPlayer? plain)
        {
            _positional = positional;
            _plain = plain;
            var polyphonic = new AudioStreamPolyphonic { Polyphony = 4 };
            if (positional != null)
            {
                positional.Stream = polyphonic;
                positional.MaxPolyphony = 4;
            }
            else
            {
                plain!.Stream = polyphonic;
            }
        }

        public bool IsBusy { get; set; }

        public void Play(SoundProfile profile, Vector3 position)
        {
            if (_playback == null)
            {
                _playback = (AudioStreamPlaybackPolyphonic)(_positional != null
                    ? _positional.GetStreamPlayback()
                    : _plain!.GetStreamPlayback());
            }
            if (_positional != null)
            {
                _positional.Position = position;
            }
            _activeStreamId = (int)_playback.PlayStream(profile.Streams!, 0, profile.VolumeDb, 1.0f,
                AudioServer.PlaybackType.Default, profile.Bus);
            IsBusy = true;
        }

        public bool IsStreamActive()
        {
            if (_positional != null)
            {
                return _positional.Playing;
            }
            return _plain!.Playing;
        }

        public void StopAllStreams()
        {
            if (_playback != null && _activeStreamId != -1)
            {
                _playback.StopStream(_activeStreamId);
            }
            _activeStreamId = -1;
            IsBusy = false;
        }

        public void OnPoolReset()
        {
            StopAllStreams();
            _playback = null;
        }
    }
}
