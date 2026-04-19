namespace Jmodot.Implementation.Movement.Strategies;

using Core.Shared.Attributes;
using Core.Stats;

/// <summary>
/// 2D movement strategy for states where input is suppressed but existing
/// velocity (from knockback/impulse) should persist and decay — hit, fall,
/// tumble, recoil, airborne states. desiredDirection is ignored; only friction
/// acts on the current velocity.
///
/// Contrast with <see cref="IdleFrictionStrategy2D"/>: recoil/airborne
/// typically uses a LOWER friction (air resistance) so impulses carry the
/// actor further. Use distinct stat bindings to author distinct game feel.
///
/// Alternative: <c>MovementProcessor2D.ProcessExternalForcesOnly()</c> covers
/// the same "no input, friction only" semantic without requiring a strategy.
/// Prefer this strategy when the state author wants to swap strategies
/// declaratively at runtime (e.g. via an [Export] on a State resource).
/// </summary>
[GlobalClass, Tool]
public partial class RecoilAirborneStrategy2D : BaseMovementStrategy2D
{
    [ExportGroup("Stat Bindings")]
    [Export, RequiredExport] private Attribute _frictionAttr = null!;

    public override Vector2 CalculateVelocity(Vector2 currentVelocity, Vector2 desiredDirection,
        Vector2 previousDirection, IStatProvider stats, float delta)
    {
        // desiredDirection is INTENTIONALLY ignored — input is suppressed during
        // recoil/airborne states. Only friction acts on existing velocity.
        var friction = stats.GetStatValue<float>(_frictionAttr);
        return currentVelocity.MoveToward(Vector2.Zero, friction * delta);
    }

    #region Test Helpers
#if TOOLS
    internal void SetFrictionAttrForTest(Attribute attr) => _frictionAttr = attr;
#endif
    #endregion
}
