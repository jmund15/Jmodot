namespace Jmodot.Implementation.Combat.Effects;

using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.EffectDefinitions;
using Jmodot.Core.Combat.Reactions;
using Jmodot.Core.Visual.Effects;
using Jmodot.Implementation.Shared;

/// <summary>
/// Combat effect that produces a <see cref="KnockbackResult"/> implementing
/// <see cref="IForceCarrier"/>. Designer-tunable BaseForce + falloff curves.
/// Direction is computed per-target as the normalized vector from
/// <see cref="HitContext.EpicenterPosition"/> to the target's world position,
/// optionally flattened to horizontal to avoid lofting.
/// </summary>
[GlobalClass]
public partial class KnockbackEffect : Resource, ICombatEffect
{
    [ExportGroup("Force")]
    [Export] public BaseFloatValueDefinition? BaseForce { get; set; }
    [Export] public Curve? DistanceFalloff { get; set; }
    [Export] public Curve? ConeAngleFalloff { get; set; }
    [Export] public bool FlattenToHorizontal { get; set; } = true;

    /// <summary>
    /// Vertical bias applied to the post-flatten radial direction. 0 (default) leaves direction
    /// unchanged — pure horizontal radial impulse, identical to pre-feature behavior. Greater
    /// than 0 rotates the direction toward Vector3.Up by the given angle, so producers like a
    /// rising rock pillar can pop targets UP+OUT instead of just OUT. At 90° the impulse is
    /// pure-vertical. The rotation runs BEFORE the zero-direction guard, so a centered target
    /// (target == epicenter) becomes pure-up rather than no-impulse — the degenerate case
    /// resolves naturally without a special branch.
    /// When this is &gt; 0 the produced KnockbackResult stamps PreserveVertical=true so receivers
    /// (KnockbackComponent3D) know the Direction.Y is intentional and skip their safety-net flatten.
    /// </summary>
    [Export(PropertyHint.Range, "0,90,1")]
    public float UpwardAngleDegrees { get; set; }

    [ExportGroup("Tags")]
    [Export] public Array<CombatTag> Tags { get; private set; } = new();
    IEnumerable<CombatTag> ICombatEffect.Tags => Tags;

    [ExportGroup("Visual")]
    [Export] public VisualEffect? Visual { get; private set; }

    private bool _curveDeferralWarned;

    public CombatResult? Apply(ICombatant target, HitContext context)
    {
        var force = ResolveForce(context);
        if (force <= 0f)
        {
            return null;
        }

        if (target.OwnerNode is not Node3D targetNode)
        {
            return null;
        }

        var direction = (targetNode.GlobalPosition - context.EpicenterPosition).Normalized();
        if (FlattenToHorizontal)
        {
            direction = new Vector3(direction.X, 0f, direction.Z).Normalized();
        }

        // Upward-angle rotation runs BEFORE the zero-guard so a centered target
        // (direction == Zero) becomes pure-up: Zero*cos + Up*sin = (0, sin, 0) → Vector3.Up
        // after normalize. The degenerate case resolves naturally as a feature.
        if (UpwardAngleDegrees > 0f)
        {
            var rad = Mathf.DegToRad(UpwardAngleDegrees);
            direction = (direction * Mathf.Cos(rad) + Vector3.Up * Mathf.Sin(rad)).Normalized();
        }

        // Degenerate: epicenter == target with no upward bias, or purely vertical
        // displacement + FlattenToHorizontal=true with no upward bias.
        if (direction == Vector3.Zero)
        {
            return null;
        }

        return new KnockbackResult
        {
            Source = context.Source,
            Target = target.OwnerNode,
            Tags = Tags,
            Direction = direction,
            Force = force,
            TriggeredLaunch = false,
            PreserveVertical = UpwardAngleDegrees > 0f,
        };
    }

    private float ResolveForce(HitContext context)
    {
        if (BaseForce is null)
        {
            return 0f;
        }

        // Q2 deferred — IStatProvider not wired; only ConstantFloatDefinition is null-safe.
        if (BaseForce is not ConstantFloatDefinition)
        {
            JmoLogger.Error(this,
                $"{nameof(KnockbackEffect)}: BaseForce must be ConstantFloatDefinition until Q2 " +
                "IStatProvider plumbing is wired — ignoring.");
            return 0f;
        }

        var baseValue = BaseForce.ResolveFloatValue(null);

        if ((DistanceFalloff is not null || ConeAngleFalloff is not null) && !_curveDeferralWarned)
        {
            _curveDeferralWarned = true;
            JmoLogger.Warning(this,
                $"{nameof(KnockbackEffect)}: DistanceFalloff/ConeAngleFalloff are wired but Q2 " +
                "input plumbing is not yet implemented; curves ignored, returning BaseForce only.");
        }

        return baseValue;
    }
}
