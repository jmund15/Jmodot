namespace Jmodot.Implementation.AI.Emotions;

using System;
using Godot;

/// <summary>
///     Curve-driven decay configuration for emotions. A single resource handles all decay shapes
///     via <see cref="EmotionDecayPreset"/> selection — no abstract hierarchies or subclasses.
///     All presets natively produce 1->0 curves (full intensity -> zero). Custom curves are
///     also authored in the intuitive 1->0 direction.
///
///     <b>CurveInfluence</b> blends between linear decay (CI=0) and full curve shape (CI=1):
///     <c>multiplier = Lerp(linearDecay, curveValue, CurveInfluence)</c>
///
///     Pattern inspired by the trail system's DecayCurvePreset/DecayCurveFactory, adapted for
///     emotion-native semantics with decay-specific preset names and 1->0 direction.
/// </summary>
[Tool]
[GlobalClass]
public partial class EmotionDecayProfile : Resource
{
    /// <summary>tan(60°) — slope used for ease-in/ease-out tangents in Hermite interpolation.</summary>
    private const float Tan60 = 1.732f;

    /// <summary>Seconds from stimulus to full decay. 0 or negative = instant full decay.</summary>
    [Export(PropertyHint.Range, "0.1,120.0,0.1")]
    public float DecayDuration
    {
        get => _decayDuration;
        private set
        {
            _decayDuration = value;
            if (Engine.IsEditorHint()) { EmitChanged(); }
        }
    }
    private float _decayDuration = 5.0f;

    /// <summary>Shape of the decay curve. Changing this regenerates the curve from the preset.</summary>
    [Export]
    public EmotionDecayPreset Preset
    {
        get => _preset;
        private set
        {
            if (_preset == value) { return; }
            _preset = value;
            if (_preset != EmotionDecayPreset.Custom)
            {
                _decayCurve = GenerateCurve(_preset);
            }
            if (Engine.IsEditorHint())
            {
                EmitChanged();
                NotifyPropertyListChanged();
            }
        }
    }
    private EmotionDecayPreset _preset = EmotionDecayPreset.Linear;

    /// <summary>
    /// The decay curve mapping normalized time (X: 0->1) to intensity multiplier (Y: 1->0).
    /// Lazy-generated from <see cref="Preset"/> on first access when not already set.
    /// For <see cref="EmotionDecayPreset.Custom"/>, assign a hand-authored 1->0 curve.
    /// </summary>
    [Export]
    public Curve? DecayCurve
    {
        get
        {
            if (_decayCurve == null && _preset != EmotionDecayPreset.Custom)
            {
                _decayCurve = GenerateCurve(_preset);
            }
            return _decayCurve;
        }
        private set => _decayCurve = value;
    }
    private Curve? _decayCurve;

    /// <summary>
    /// Blends between linear decay (0) and full curve shape (1).
    /// Formula: <c>multiplier = (1 - CI) * linearDecay + CI * curveValue</c>.
    /// At 0 = boring linear fade. At 1 = full curve shape. Designer-friendly "stylization" knob.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CurveInfluence
    {
        get => _curveInfluence;
        private set
        {
            _curveInfluence = value;
            if (Engine.IsEditorHint()) { EmitChanged(); }
        }
    }
    private float _curveInfluence = 1.0f;

    /// <summary>Toggle to disable decay entirely (intensity stays at base forever).</summary>
    [Export]
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Calculates the current intensity given an initial base intensity and elapsed time since stimulus.
    /// Pure function — safe to call from any thread, no side effects.
    /// </summary>
    /// <param name="baseIntensity">Intensity at the moment of stimulus (after amplification).</param>
    /// <param name="elapsed">Seconds since the stimulus, accumulated via delta.</param>
    /// <returns>Current effective intensity in [0, baseIntensity] range.</returns>
    public float CalculateIntensity(float baseIntensity, float elapsed)
    {
        if (!IsEnabled || _decayDuration <= 0f) { return baseIntensity; }
        if (elapsed <= 0f) { return baseIntensity; }
        if (elapsed >= _decayDuration) { return _preset == EmotionDecayPreset.Permanent ? baseIntensity : 0f; }

        return CalculateDecay(baseIntensity, elapsed, _decayDuration, _curveInfluence, DecayCurve, _preset);
    }

    /// <summary>
    /// Pure static decay calculation extracted for testability.
    /// </summary>
    internal static float CalculateDecay(
        float baseIntensity, float elapsed, float duration,
        float curveInfluence, Curve? curve, EmotionDecayPreset preset)
    {
        if (preset == EmotionDecayPreset.Permanent) { return baseIntensity; }

        float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
        float linearDecay = 1f - t;
        float curveValue = curve?.Sample(t) ?? linearDecay;
        float multiplier = (1f - curveInfluence) * linearDecay + curveInfluence * curveValue;

        return baseIntensity * Mathf.Max(multiplier, 0f);
    }

    /// <summary>
    /// Generates a native 1->0 decay curve for the given preset.
    /// Uses 2-point Hermite interpolation with exact mathematical tangents.
    /// </summary>
    internal static Curve GenerateCurve(EmotionDecayPreset preset)
    {
        return preset switch
        {
            EmotionDecayPreset.Linear => CreateLinearDecay(),
            EmotionDecayPreset.SharpFade => CreateSharpFade(),
            EmotionDecayPreset.LingeringFade => CreateLingeringFade(),
            EmotionDecayPreset.PlateauDrop => CreatePlateauDrop(),
            EmotionDecayPreset.SmoothFade => CreateSmoothFade(),
            EmotionDecayPreset.Permanent => CreatePermanent(),
            EmotionDecayPreset.Custom => throw new ArgumentException(
                "Custom preset requires a manually-assigned DecayCurve.", nameof(preset)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }

    /// <summary>Straight diagonal 1->0. At t=0: y=1, dy/dx=-1. At t=1: y=0, dy/dx=-1.</summary>
    private static Curve CreateLinearDecay()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, -1f);
        curve.AddPoint(new Vector2(1f, 0f), -1f, 0f);
        return curve;
    }

    /// <summary>Fast initial drop, slow lingering tail (1->0). Steep at start, flat at end.</summary>
    private static Curve CreateSharpFade()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, -Tan60);
        curve.AddPoint(new Vector2(1f, 0f), 0f, 0f);
        return curve;
    }

    /// <summary>Holds steady, then drops off quickly (1->0). Flat at start, steep at end.</summary>
    private static Curve CreateLingeringFade()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 0f), -Tan60, 0f);
        return curve;
    }

    /// <summary>
    /// Holds at max, cliff-drops to zero (1->0).
    /// Four points: hold at 1.0, smooth transition, drop to 0, hold at 0.
    /// </summary>
    private static Curve CreatePlateauDrop()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(0.3f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(0.5f, 0f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 0f), 0f, 0f);
        return curve;
    }

    /// <summary>S-curve: slow start, fast middle, slow end (smoothstep 1->0).</summary>
    private static Curve CreateSmoothFade()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 0f), 0f, 0f);
        return curve;
    }

    /// <summary>Flat 1.0 — intensity never decays.</summary>
    private static Curve CreatePermanent()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f), 0f, 0f);
        curve.AddPoint(new Vector2(1f, 1f), 0f, 0f);
        return curve;
    }

    #region Test Helpers

    internal void SetDecayDuration(float value) => _decayDuration = value;
    internal void SetPreset(EmotionDecayPreset preset) => _preset = preset;
    internal void SetDecayCurve(Curve? curve) => _decayCurve = curve;
    internal void SetCurveInfluence(float value) => _curveInfluence = value;
    internal void SetIsEnabled(bool value) => IsEnabled = value;

    #endregion
}
