namespace Jmodot.Implementation.Movement.Strategies;

using Core.Combat.EffectDefinitions;
using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
///     Linear movement strategy using MoveToward for constant-rate acceleration and friction.
///     Speed is explicitly capped by a maxSpeed value — contrasts with TerminalVelocityMovementStrategy3D
///     where max speed emerges from the acceleration/friction ratio.
///
///     <para><b>C15 / Wave 3b (2026-04-26):</b> migrated from raw <c>Attribute</c> exports to
///     <see cref="BaseFloatValueDefinition"/>. Designers wire <see cref="ConstantFloatDefinition"/>
///     for stat-free strategies or <see cref="AttributeFloatDefinition"/> for stat-sourced.
///     Behavior is preserved: <c>AttributeFloatDefinition.ResolveFloatValue(stats)</c> internally
///     calls <c>stats.GetStatValue&lt;float&gt;(SourceAttribute)</c>.</para>
/// </summary>
[GlobalClass, Tool]
public partial class LinearMovementStrategy3D : BaseMovementStrategy3D
{
    [ExportGroup("Value Definitions")]
    [Export, RequiredExport] private BaseFloatValueDefinition _maxSpeed = null!;
    [Export, RequiredExport] private BaseFloatValueDefinition _acceleration = null!;
    [Export, RequiredExport] private BaseFloatValueDefinition _friction = null!;

    public override Vector3 CalculateVelocity(Vector3 currentVelocity, Vector3 desiredDirection, Vector3 previousDirection, IStatProvider stats, float delta)
    {
        var maxSpeed = _maxSpeed.ResolveFloatValue(stats);
        var targetVelocity = desiredDirection * maxSpeed;
        var newVelocity = currentVelocity;

        if (!desiredDirection.IsZeroApprox())
        {
            newVelocity = newVelocity.MoveToward(targetVelocity,
                _acceleration.ResolveFloatValue(stats) * delta);
        }
        else
        {
            newVelocity = newVelocity.MoveToward(Vector3.Zero,
                _friction.ResolveFloatValue(stats) * delta);
        }

        return newVelocity;
    }
}
