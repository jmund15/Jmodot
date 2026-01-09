namespace Jmodot.Core.Combat.EffectDefinitions;

using Stats;

/// <summary>
/// Abstract base class for all float value definitions.
/// Provides a common interface for resolving float values, optionally using stats.
/// </summary>
[GlobalClass]
public abstract partial class BaseFloatValueDefinition : Resource
{
    /// <summary>
    /// Resolves the final float value, optionally using the provided stat provider.
    /// </summary>
    public abstract float ResolveFloatValue(IStatProvider? stats);
}
