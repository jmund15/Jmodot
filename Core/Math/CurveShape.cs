namespace Jmodot.Core.Math;

using Godot;

/// <summary>
/// Composable preset-driven curve resource. A pure math building block that maps
/// a normalized input t ∈ [0,1] to a blended multiplier (0→1).
///
/// Agnostic to what the input represents — the consumer normalizes their input
/// into [0,1] before sampling.
///
/// X = normalized input (0→1), Y = multiplier (0→1).
/// </summary>
[Tool]
[GlobalClass]
public partial class CurveShape : Resource
{
    /// <summary>
    /// Preset for the curve shape.
    /// </summary>
    [Export]
    public CurveShapePreset Preset
    {
        get => _preset;
        private set
        {
            if (_preset == value) { return; }
            _preset = value;
            ApplyPreset();
            if (Engine.IsEditorHint())
            {
                EmitChanged();
                NotifyPropertyListChanged();
            }
        }
    }

    private CurveShapePreset _preset = CurveShapePreset.Constant;

    /// <summary>
    /// The curve mapping normalized input (X: 0→1) to multiplier (Y: 0→1).
    /// Lazy-initialized from <see cref="Preset"/> on first access.
    /// </summary>
    /// <remarks>
    /// The getter assigns <c>_curve</c> on first access when the preset is non-Custom
    /// and no curve has been set. This is intentional lazy-init, not an accidental
    /// mutation: Resources have no _Ready hook, and a .tres whose Preset matches the
    /// default value may never trigger the Preset setter during load. Assigning once
    /// here means subsequent reads are O(1) and observably pure. Callers that want
    /// deterministic timing should invoke <see cref="ApplyPreset"/> explicitly at
    /// load time.
    /// </remarks>
    [Export]
    public Curve? Curve
    {
        get
        {
            if (_curve == null && _preset != CurveShapePreset.Custom)
            {
                _curve = CurveShapeFactory.Create(_preset, _invert);
            }
            return _curve;
        }
        private set => _curve = value;
    }

    private Curve? _curve;

    /// <summary>
    /// Blends the curve shape with a uniform value. 0 = curve ignored (always 1.0),
    /// 1 = full curve influence. Formula: multiplier = (1-Influence) + Influence × curveValue.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Influence { get; private set; } = 1.0f;

    /// <summary>
    /// X-reflects the preset curve. Regenerates from preset when changed.
    /// No effect on Custom preset.
    /// </summary>
    [Export]
    public bool Invert
    {
        get => _invert;
        private set
        {
            if (_invert == value) { return; }
            _invert = value;
            ApplyPreset();
            if (Engine.IsEditorHint())
            {
                EmitChanged();
            }
        }
    }

    private bool _invert;

    /// <summary>
    /// Returns the blended curve multiplier for the given normalized input.
    /// Returns 1.0 when curve is null (no shaping, flat value).
    /// </summary>
    public float GetMultiplier(float t)
    {
        if (Curve == null) { return 1f; }
        float rawCurve = Curve.Sample(Mathf.Clamp(t, 0f, 1f));
        return (1f - Influence) + Influence * rawCurve;
    }

    /// <summary>
    /// Generates a curve from the current preset and assigns it.
    /// No-op for Custom preset.
    /// </summary>
    public void ApplyPreset()
    {
        if (_preset == CurveShapePreset.Custom) { return; }
        _curve = CurveShapeFactory.Create(_preset, _invert);
    }

    #region Test Helpers
#if TOOLS

    internal void SetPreset(CurveShapePreset preset) => _preset = preset;
    internal void SetCurve(Curve? curve) => _curve = curve;
    internal void SetInfluence(float influence) => Influence = influence;
    internal void SetInvert(bool invert) => _invert = invert;

    /// <summary>Expose GetMultiplier for direct unit testing.</summary>
    internal float TestGetMultiplier(float t) => GetMultiplier(t);

#endif
    #endregion
}
