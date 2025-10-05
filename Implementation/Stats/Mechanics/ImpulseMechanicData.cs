namespace Jmodot.Implementation.Stats.Mechanics;

using Core.Stats;
using Core.Stats.Mechanics;

/// <summary>
/// Defines the contract for a simple, instantaneous physics impulse like a jump or dodge.
/// It requires one Attribute to define its strength.
/// </summary>
[GlobalClass]
public partial class ImpulseMechanicData : MechanicData
{
    [ExportGroup("Attribute Contract")]
    [Export]
    public Attribute ImpulseStrengthAttribute { get; private set; } = null!;
}
