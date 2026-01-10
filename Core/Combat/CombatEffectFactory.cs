namespace Jmodot.Core.Combat;

using EffectDefinitions;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Visual.Effects;
using Stats;

/// <summary>
/// Base class for Combat Effect Creation.
/// Desiging the factory/create pattern allows for resource to be stateless
/// while maintaining resource/exportability in the Godot editor
/// </summary>
[GlobalClass]
public abstract partial class CombatEffectFactory : Resource, ICombatEffectFactory
{
    [Export] public VisualEffect? TargetVisualEffect { get; set; }

    public abstract ICombatEffect Create(Jmodot.Core.Stats.IStatProvider? stats = null);
}
