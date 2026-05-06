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

        // Degenerate: epicenter == target, or purely vertical displacement + FlattenToHorizontal=true.
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
