#region

using Jmodot.Core.Stats.Mechanics;

#endregion

namespace Jmodot.Implementation.Stats.Mechanics;

/// <summary>
///     Defines the data for a simple, instantaneous physics impulse(e.g., a jump).
/// </summary>
[GlobalClass]
public partial class ImpulseMechanicData : MechanicData // Inherits from the base class
{
    [Export] public float Strength { get; private set; } = 10.0f;
}