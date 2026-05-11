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
        // Horizontal-plane navigation only. Y is owned by external forces (gravity) and
        // impulses (knockback). The strategy must NEVER touch Y — Vector3.MoveToward
        // operates per-component, so a target with Y=0 silently drags the input Y toward
        // zero every frame, erasing both impulses and gravity in the same tick.
        // Pre-v6.1 violation broke the HSM-observes-physics contract: KnockedUpState
        // never entered because Velocity.Y was perpetually ≈0.
        var maxSpeed = _maxSpeed.ResolveFloatValue(stats);
        var horizontalCurrent = new Vector3(currentVelocity.X, 0f, currentVelocity.Z);
        var horizontalTarget = !desiredDirection.IsZeroApprox()
            ? new Vector3(desiredDirection.X, 0f, desiredDirection.Z) * maxSpeed
            : Vector3.Zero;
        var rate = !desiredDirection.IsZeroApprox()
            ? _acceleration.ResolveFloatValue(stats)
            : _friction.ResolveFloatValue(stats);

        var newHorizontal = horizontalCurrent.MoveToward(horizontalTarget, rate * delta);
        return new Vector3(newHorizontal.X, currentVelocity.Y, newHorizontal.Z);
    }
}
