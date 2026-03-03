namespace Jmodot.Implementation.AI.Emotions;

using System;
using System.Collections.Generic;
using System.Linq;
using Affinities;
using BB;
using Core.AI;
using Core.AI.Affinities;
using Core.AI.BB;
using Core.AI.Emotions;
using Core.Components;
using Shared;

/// <summary>
///     Main runtime component for an entity's emotional state. Receives stimuli from
///     bridges (threat detection, ingredient proximity, etc.), amplifies them through
///     the personality system, and maintains decaying <see cref="EmotionInstance"/>s.
///
///     Consumers (HSM conditions, Utility AI, Steering) read via <see cref="IEmotionalStateProvider"/>.
///     Registered on the blackboard as <see cref="BBDataSig.EmotionalState"/>.
///
///     Per-frame cost: one float addition per active emotion (delta accumulation in Tick).
///     Decay math is computed on-demand when intensity is queried.
/// </summary>
[GlobalClass]
public partial class AIEmotionalStateComponent : Node, IComponent, IBlackboardProvider, IEmotionalStateProvider, IDebugPanelProvider
{
    /// <summary>Fallback decay profile when no per-stimulus override is provided.</summary>
    [Export] public EmotionDecayProfile? DefaultDecayProfile { get; private set; }

    /// <summary>Optional personality-driven amplification. Null = raw intensity used as-is.</summary>
    [Export] public EmotionAmplificationProfile? AmplificationProfile { get; private set; }

    /// <summary>Seconds between inactive emotion cleanup sweeps.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.5")]
    public float CleanupInterval { get; private set; } = 2.0f;

    private readonly Dictionary<EmotionType, EmotionInstance> _emotions = new();
    private AIAffinitiesComponent? _affinities;
    private float _cleanupTimer;

    #region IComponent

    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        if (IsInitialized) { return true; }

        // Optional: resolve affinities for personality amplification
        bb.TryGet<AIAffinitiesComponent>(BBDataSig.Affinities, out _affinities);

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;

    #endregion

    #region IBlackboardProvider

    public (StringName Key, object Value)? Provision => (BBDataSig.EmotionalState, this);

    #endregion

    #region IEmotionalStateProvider

    /// <inheritdoc />
    public float? GetIntensity(EmotionType type)
    {
        if (!_emotions.TryGetValue(type, out var instance)) { return null; }
        if (!instance.IsActive) { return null; }
        return instance.CurrentIntensity;
    }

    /// <inheritdoc />
    public bool TryGetIntensity(EmotionType type, out float intensity)
    {
        if (_emotions.TryGetValue(type, out var instance) && instance.IsActive)
        {
            intensity = instance.CurrentIntensity;
            return true;
        }

        intensity = 0f;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<(EmotionType Type, float Intensity)> GetAllActiveEmotions()
    {
        foreach (var (type, instance) in _emotions)
        {
            if (instance.IsActive)
            {
                yield return (type, instance.CurrentIntensity);
            }
        }
    }

    /// <inheritdoc />
    public event Action<EmotionType, float, float> EmotionChanged = delegate { };

    #endregion

    /// <summary>
    /// Primary input: receives a raw stimulus and creates or refreshes an emotion.
    /// Amplifies through personality if an <see cref="AmplificationProfile"/> is assigned.
    /// </summary>
    /// <param name="type">The emotion to stimulate.</param>
    /// <param name="rawIntensity">Raw stimulus intensity before personality amplification.</param>
    /// <param name="profileOverride">Optional per-stimulus decay profile. Null = use DefaultDecayProfile.</param>
    public void Stimulate(EmotionType type, float rawIntensity, EmotionDecayProfile? profileOverride = null)
    {
        float amplified = AmplifyIntensity(type, rawIntensity);
        var profile = profileOverride ?? DefaultDecayProfile ?? CreateFallbackProfile();

        if (_emotions.TryGetValue(type, out var existing))
        {
            float oldIntensity = existing.CurrentIntensity;
            existing.Refresh(amplified, profile);
            float newIntensity = existing.CurrentIntensity;
            FireIfChanged(type, oldIntensity, newIntensity);
        }
        else
        {
            var instance = new EmotionInstance(type, amplified, profile);
            _emotions[type] = instance;
            EmotionChanged.Invoke(type, 0f, amplified);
        }
    }

    /// <summary>
    /// Called each frame. Ticks all active emotions and periodically sweeps inactive ones.
    /// </summary>
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        TickEmotions(dt);

        _cleanupTimer += dt;
        if (_cleanupTimer >= CleanupInterval)
        {
            _cleanupTimer = 0f;
            SweepInactive();
        }
    }

    private float AmplifyIntensity(EmotionType type, float rawIntensity)
    {
        if (AmplificationProfile == null || _affinities == null) { return rawIntensity; }

        var linkedAffinity = AmplificationProfile.GetLinkedAffinity(type);
        if (linkedAffinity == null) { return rawIntensity; }

        if (!_affinities.TryGetAffinity(linkedAffinity, out float affinityValue)) { return rawIntensity; }

        return AmplificationProfile.GetAmplifiedIntensity(type, rawIntensity, affinityValue);
    }

    private void TickEmotions(float delta)
    {
        foreach (var (_, instance) in _emotions)
        {
            instance.Tick(delta);
        }
    }

    private void SweepInactive()
    {
        List<EmotionType>? toRemove = null;
        foreach (var (type, instance) in _emotions)
        {
            if (!instance.IsActive)
            {
                toRemove ??= new List<EmotionType>();
                toRemove.Add(type);
            }
        }

        if (toRemove == null) { return; }

        foreach (var type in toRemove)
        {
            _emotions.Remove(type);
        }
    }

    private EmotionDecayProfile CreateFallbackProfile()
    {
        var profile = new EmotionDecayProfile();
        profile.SetDecayDuration(5f);
        profile.SetPreset(EmotionDecayPreset.Linear);
        return profile;
    }

    private void FireIfChanged(EmotionType type, float oldIntensity, float newIntensity)
    {
        if (Mathf.Abs(oldIntensity - newIntensity) >= 0.001f)
        {
            EmotionChanged.Invoke(type, oldIntensity, newIntensity);
        }
    }

    #region IDebugPanelProvider

    public string DebugTabName => "Emotions";
    public int DebugTabOrder => 20;
    public bool HasDebugData => _emotions.Values.Any(e => e.IsActive);

    private VBoxContainer? _debugContainer;
    private readonly Dictionary<EmotionType, (HBoxContainer Row, ColorRect Bar, Label ValueLabel)> _debugRows = new();
    private const float BAR_MAX_WIDTH = 200f;

    public Control CreateDebugContent()
    {
        _debugContainer = new VBoxContainer { Name = "EmotionDebugContent" };
        _debugRows.Clear();

        foreach (var (type, instance) in _emotions)
        {
            if (instance.IsActive)
            {
                AddDebugRow(type, instance.CurrentIntensity);
            }
        }

        return _debugContainer;
    }

    public void UpdateDebugContent(double delta)
    {
        if (_debugContainer == null) { return; }

        // Update existing rows
        foreach (var (type, instance) in _emotions)
        {
            if (instance.IsActive)
            {
                if (_debugRows.TryGetValue(type, out var row))
                {
                    row.Bar.CustomMinimumSize = new Vector2(instance.CurrentIntensity * BAR_MAX_WIDTH, row.Bar.CustomMinimumSize.Y);
                    row.ValueLabel.Text = $"{instance.CurrentIntensity:F2}";
                }
                else
                {
                    AddDebugRow(type, instance.CurrentIntensity);
                }
            }
            else if (_debugRows.ContainsKey(type))
            {
                RemoveDebugRow(type);
            }
        }
    }

    public void OnDebugContentHidden()
    {
        // No expensive work to pause
    }

    private void AddDebugRow(EmotionType type, float intensity)
    {
        if (_debugContainer == null) { return; }

        var row = new HBoxContainer { Name = $"EmotionRow_{type.EmotionName}" };

        var nameLabel = new Label
        {
            Text = type.EmotionName,
            CustomMinimumSize = new Vector2(80, 20)
        };
        row.AddChild(nameLabel);

        var bar = new ColorRect
        {
            Color = type.DebugColor,
            CustomMinimumSize = new Vector2(intensity * BAR_MAX_WIDTH, 16)
        };
        row.AddChild(bar);

        var valueLabel = new Label { Text = $"{intensity:F2}" };
        row.AddChild(valueLabel);

        _debugContainer.AddChild(row);
        _debugRows[type] = (row, bar, valueLabel);
    }

    private void RemoveDebugRow(EmotionType type)
    {
        if (!_debugRows.TryGetValue(type, out var row)) { return; }
        row.Row.QueueFree();
        _debugRows.Remove(type);
    }

    #endregion

    #region Test Helpers

    internal void TestTick(float delta) => TickEmotions(delta);
    internal void SetAmplificationProfile(EmotionAmplificationProfile? profile) => AmplificationProfile = profile;
    internal void SetCleanupInterval(float interval) => CleanupInterval = interval;

    #endregion
}
