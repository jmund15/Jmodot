namespace Jmodot.Implementation.Movement.Acceleration;

using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;

/// <summary>
/// Composable convergence profile for horizontal velocity. A consumer that has already
/// computed a horizontal TARGET velocity hands it here together with the current horizontal
/// velocity; the profile returns the next-frame horizontal velocity, rate-limited so the
/// approach takes time instead of snapping. A null profile reference means "snap" — the
/// consumer assigns its target outright and never calls in here.
///
/// <para>The rate is authored as a <see cref="BaseFloatValueDefinition"/> so it resolves
/// through the stat pipeline like every other authored magnitude: wire
/// <see cref="ConstantFloatDefinition"/> for stat-free authoring or
/// <see cref="AttributeFloatDefinition"/> for stat-sourced.</para>
///
/// <para>Deliberately free of projectile knowledge — it takes and returns plain horizontal
/// velocity vectors, so any strategy whose output is a target velocity can adopt it without
/// the family gaining a shared export.</para>
///
/// <para><b>Y is not this type's business.</b> Callers pass XZ-only vectors and splice their
/// own Y back in. <see cref="Vector3.MoveToward"/> works per component, so a target carrying
/// Y=0 would drag a real Y toward zero every frame and erase gravity and impulses alike.
/// Dimension-parallel sibling: <see cref="AccelerationProfile2D"/>.</para>
/// </summary>
[GlobalClass, Tool]
public partial class AccelerationProfile3D : Resource
{
    [ExportGroup("Value Definitions")]
    [Export, RequiredExport] private BaseFloatValueDefinition _rate = null!;

    /// <summary>
    /// Moves <paramref name="currentHorizontal"/> toward <paramref name="targetHorizontal"/>
    /// by the resolved rate times <paramref name="delta"/>.
    /// </summary>
    /// <param name="currentHorizontal">Current horizontal velocity (XZ; Y must be zero).</param>
    /// <param name="targetHorizontal">Desired horizontal velocity (XZ; Y must be zero).</param>
    /// <param name="stats">Stat provider used to resolve the rate.</param>
    /// <param name="delta">Physics delta time.</param>
    /// <returns>The next-frame horizontal velocity.</returns>
    public Vector3 Converge(Vector3 currentHorizontal, Vector3 targetHorizontal, IStatProvider? stats, float delta)
    {
        var rate = _rate.ResolveFloatValue(stats);
        return currentHorizontal.MoveToward(targetHorizontal, rate * delta);
    }
}
