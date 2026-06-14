namespace Jmodot.Core.Combat;

using Godot;

/// <summary>
/// Converts a <see cref="HitContext2D"/> into a <see cref="HitContext"/> so the
/// dimension-agnostic <c>ICombatant.ProcessPayload</c> surface can consume 2D hits.
/// 2D-specific vectors are zero-padded on the 3D axes; 2D-aware effects should read the
/// richer <see cref="HitContext2D"/> directly. Extracted to a plain static class (no Godot
/// base type) so the field-copy parity — including <see cref="HitContext.HitSeed"/> — is
/// pure-CLR testable without instantiating a Node.
/// </summary>
public static class HitContextAdapter
{
    public static HitContext To3D(HitContext2D c) => new HitContext
    {
        Attacker = c.Attacker,
        Source = c.Source,
        HitDirection = new Vector3(c.HitDirection.X, 0f, c.HitDirection.Y),
        ImpactVelocity = new Vector3(c.ImpactVelocity.X, 0f, c.ImpactVelocity.Y),
        EpicenterPosition = new Vector3(c.EpicenterPosition.X, 0f, c.EpicenterPosition.Y),
        DistanceFromEpicenter = c.DistanceFromEpicenter,
        Kind = c.Kind,
        HitSeed = c.HitSeed,
    };
}
