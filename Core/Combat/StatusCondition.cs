using Godot;

namespace Jmodot.Core.Combat;

/// <summary>
/// Defines a condition for a Status Effect (e.g., Stop when health full, Stop when hit).
/// </summary>
public partial class StatusCondition : Resource
{
    public virtual bool Check(ICombatant target, HitContext context)
    {
        return false;
    }
}
