namespace Jmodot.Implementation.Stats.Mechanics;

using Core.Stats.Mechanics;

/// <summary>
///     Defines the data for a continuous dash or lunge mechanic.
/// </summary>
[GlobalClass]
public partial class DashMechanicData : MechanicData // Also inherits from the base class
{
    [Export] public float Speed { get; private set; } = 50.0f;

    [Export(PropertyHint.Range, "0.1, 2.0, 0.05")]
    public float Duration { get; private set; } = 0.2f;

    // You could add more properties specific to dashing
    [Export] public Curve EasingCurve { get; private set; } = null!;
    [Export] public bool IgnoreGravityDuringDash { get; private set; } = true;
}
