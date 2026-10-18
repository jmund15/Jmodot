namespace Jmodot.Implementation.Combat;

using Godot;
using Jmodot.Core.Shared;

/// <summary>Maps a resolved push's impulse and speed change to a victim-only pause.</summary>
[GlobalClass, Tool]
public partial class KnockbackHoldConfig : Resource, IResourceConfigurationWarnings
{
    /// <summary>Impulse share of strength: 1 uses only impulse, 0 only speed change.</summary>
    [ExportGroup("Strength")]
    [Export(PropertyHint.Range, "0,1,0.01")] public float ImpulseWeight { get => _impulseWeight; set { _impulseWeight = value; EmitChanged(); } }
    private float _impulseWeight = 0.5f;
    /// <summary>Resolved impulse in N·s that counts as full impulse strength. Must be finite and positive.</summary>
    [Export(PropertyHint.Range, "0.01,200,0.01,or_greater,suffix:N·s")] public float ImpulseReference { get => _impulseReference; set { _impulseReference = value; EmitChanged(); } }
    private float _impulseReference = 30f;
    /// <summary>Resolved speed change in m/s that counts as full speed strength. Must be finite and positive.</summary>
    [Export(PropertyHint.Range, "0.01,100,0.01,or_greater,suffix:m/s")] public float SpeedChangeReference { get => _speedChangeReference; set { _speedChangeReference = value; EmitChanged(); } }
    private float _speedChangeReference = 12f;
    /// <summary>Blended strength below this causes no pause. Must be finite, at least 0 and less than 1.</summary>
    [Export(PropertyHint.Range, "0,0.99,0.01")] public float DeadZone { get => _deadZone; set { _deadZone = value; EmitChanged(); } }
    private float _deadZone;
    /// <summary>Required curve from strength above the dead zone to a fraction of the maximum pause. New configs start with a straight line.</summary>
    [ExportGroup("Pause")]
    [Export] public Curve? Shape { get => _shape; set { _shape = value; EmitChanged(); } }
    private Curve? _shape = LinearCurve();
    /// <summary>Maximum real seconds from the first push, including extensions. Must be finite and positive.</summary>
    [Export(PropertyHint.Range, "0.01,2,0.01,or_greater,suffix:s")] public float MaxSeconds { get => _maxSeconds; set { _maxSeconds = value; EmitChanged(); } }
    private float _maxSeconds = 0.2f;
    /// <summary>A true blackboard bool prevents or ends the pause. Empty disables suppression; committed attacks may freeze.</summary>
    [Export] public StringName SuppressWhileFlagKey { get => _suppressWhileFlagKey; set { _suppressWhileFlagKey = value; EmitChanged(); } }
    private StringName _suppressWhileFlagKey = "";

    public string[] GetResourceConfigurationWarnings()
    {
        var warnings = Validate();
        if (SuppressWhileFlagKey.IsEmpty)
        {
            warnings.Add("SuppressWhileFlagKey is empty: suppression is disabled; committed attacks may freeze.");
        }
        return warnings.ToArray();
    }

    /// <summary>The same rules as <see cref="Validate"/>, without building its warning strings: read on every resolved push.</summary>
    internal bool IsValid => float.IsFinite(ImpulseWeight) && ImpulseWeight is >= 0f and <= 1f
        && float.IsFinite(ImpulseReference) && ImpulseReference > 0f
        && float.IsFinite(SpeedChangeReference) && SpeedChangeReference > 0f
        && float.IsFinite(DeadZone) && DeadZone is >= 0f and < 1f
        && Shape != null
        && float.IsFinite(MaxSeconds) && MaxSeconds > 0f;

    private static Curve LinearCurve()
    {
        var curve = new Curve();
        curve.AddPoint(Vector2.Zero);
        curve.AddPoint(Vector2.One);
        curve.SetPointRightMode(0, Curve.TangentMode.Linear);
        curve.SetPointLeftMode(1, Curve.TangentMode.Linear);
        return curve;
    }

    private System.Collections.Generic.List<string> Validate()
    {
        var warnings = new System.Collections.Generic.List<string>();
        if (!float.IsFinite(ImpulseWeight) || ImpulseWeight < 0f || ImpulseWeight > 1f)
        {
            warnings.Add("ImpulseWeight must be finite and in [0, 1].");
        }
        if (!float.IsFinite(ImpulseReference) || !(ImpulseReference > 0f))
        {
            warnings.Add("ImpulseReference must be finite and positive (N·s).");
        }
        if (!float.IsFinite(SpeedChangeReference) || !(SpeedChangeReference > 0f))
        {
            warnings.Add("SpeedChangeReference must be finite and positive (m/s).");
        }
        if (!float.IsFinite(DeadZone) || DeadZone < 0f || DeadZone >= 1f)
        {
            warnings.Add("DeadZone must be finite, at least 0 and less than 1.");
        }
        if (Shape == null) { warnings.Add("Shape is required to map strength to pause duration."); }
        if (!float.IsFinite(MaxSeconds) || !(MaxSeconds > 0f))
        {
            warnings.Add("MaxSeconds must be finite and positive (real seconds).");
        }
        return warnings;
    }
}
