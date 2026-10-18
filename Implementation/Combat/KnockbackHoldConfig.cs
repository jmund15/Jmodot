namespace Jmodot.Implementation.Combat;

using Godot;
using Jmodot.Core.Shared;

/// <summary>Maps a resolved push's impulse and speed change to a victim-only pause.</summary>
[GlobalClass, Tool]
public partial class KnockbackHoldConfig : Resource, IResourceConfigurationWarnings
{
    /// <summary>Impulse share of strength: 1 uses only impulse, 0 only speed change.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float ImpulseWeight { get => _impulseWeight; set { _impulseWeight = value; EmitChanged(); } }
    private float _impulseWeight = 0.5f;
    /// <summary>Resolved impulse in N·s that counts as full impulse strength. Must be finite and positive.</summary>
    [Export] public float ImpulseReference { get => _impulseReference; set { _impulseReference = value; EmitChanged(); } }
    private float _impulseReference;
    /// <summary>Resolved speed change in m/s that counts as full speed strength. Must be finite and positive.</summary>
    [Export] public float SpeedChangeReference { get => _speedChangeReference; set { _speedChangeReference = value; EmitChanged(); } }
    private float _speedChangeReference;
    /// <summary>Blended strength below this causes no pause. Must be finite, at least 0 and less than 1.</summary>
    [Export(PropertyHint.Range, "0,0.99,0.01")] public float DeadZone { get => _deadZone; set { _deadZone = value; EmitChanged(); } }
    private float _deadZone;
    /// <summary>Required curve from strength above the dead zone to a fraction of the maximum pause.</summary>
    [Export] public Curve? Shape { get => _shape; set { _shape = value; EmitChanged(); } }
    private Curve? _shape;
    /// <summary>Maximum real seconds from the first push, including extensions. Must be finite and positive.</summary>
    [Export] public float MaxSeconds { get => _maxSeconds; set { _maxSeconds = value; EmitChanged(); } }
    private float _maxSeconds;
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

    internal bool IsValid => Validate().Count == 0;

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
