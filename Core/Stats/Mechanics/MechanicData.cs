namespace Jmodot.Core.Stats.Mechanics;

/// <summary>
///     The abstract base class for all mechanic data resources. Its sole purpose is to
///     provide a common, type-safe contract for different types of character actions.
///     Concrete implementations (e.g., ImpulseMechanicData, DashMechanicData) will
///     define the specific properties for each action.
/// </summary>
[GlobalClass]
public abstract partial class MechanicData : Resource
{
    // This class is intentionally empty. It's a "marker" base class.
}