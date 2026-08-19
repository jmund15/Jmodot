namespace Jmodot.Implementation.Movement.Acceleration;

using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;

/// <summary>
/// Composable convergence profile for velocity. A consumer that has already computed a TARGET
/// velocity hands it here together with the current velocity; the profile returns the
/// next-frame velocity, rate-limited so the approach takes time instead of snapping. A null
/// profile reference means "snap" — the consumer assigns its target outright and never calls
/// in here.
///
/// <para>The rate is authored as a <see cref="BaseFloatValueDefinition"/> so it resolves
/// through the stat pipeline like every other authored magnitude: wire
/// <see cref="ConstantFloatDefinition"/> for stat-free authoring or
/// <see cref="AttributeFloatDefinition"/> for stat-sourced.</para>
///
/// <para>Deliberately free of projectile knowledge — it takes and returns plain velocity
/// vectors, so any strategy whose output is a target velocity can adopt it without the family
/// gaining a shared export.</para>
///
/// <para><b>Any axis the caller must preserve is the caller's business.</b>
/// <see cref="Vector2.MoveToward"/> works per component, so a target carrying zero on an axis
/// that currently holds a real value — a platformer's gravity or a knockback impulse — would
/// drag that axis to zero every frame. Pass only the axes being converged and splice the rest
/// back afterwards. Dimension-parallel sibling: <see cref="AccelerationProfile3D"/>.</para>
/// </summary>
[GlobalClass, Tool]
public partial class AccelerationProfile2D : Resource
{
    [ExportGroup("Value Definitions")]
    [Export, RequiredExport] private BaseFloatValueDefinition _rate = null!;

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> by the resolved rate
    /// times <paramref name="delta"/>.
    /// </summary>
    /// <param name="current">Current velocity, restricted to the axes being converged.</param>
    /// <param name="target">Desired velocity, on those same axes.</param>
    /// <param name="stats">Stat provider used to resolve the rate.</param>
    /// <param name="delta">Physics delta time.</param>
    /// <returns>The next-frame velocity on the converged axes.</returns>
    public Vector2 Converge(Vector2 current, Vector2 target, IStatProvider? stats, float delta)
    {
        var rate = _rate.ResolveFloatValue(stats);
        return current.MoveToward(target, rate * delta);
    }
}
