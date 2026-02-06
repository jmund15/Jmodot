namespace Jmodot.Core.Combat.EffectDefinitions;

using Godot;
using Stats;

/// <summary>
/// Abstract base class for all integer value definitions.
/// Provides a common interface for resolving int values, optionally using stats.
/// Mirrors <see cref="BaseFloatValueDefinition"/> for integer use cases.
/// </summary>
[Tool]
[GlobalClass]
public abstract partial class BaseIntValueDefinition : Resource
{
    /// <summary>
    /// Resolves the final integer value, optionally using the provided stat provider.
    /// </summary>
    public abstract int ResolveIntValue(IStatProvider? stats);
}
