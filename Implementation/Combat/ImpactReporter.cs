namespace Jmodot.Implementation.Combat;

using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Health;
using Actors;
using Core.Identification;
using AI.BB;
using Shared;

/// <summary>
/// One hit being applied: the victim's health when it began and every result its effects produced.
/// A scope opened inside another one collects into the outer scope, which alone publishes.
/// </summary>
internal sealed class PayloadScope
{
    internal PayloadScope(HitContext context, float healthBefore, int appliedPushesBefore, PayloadScope? outer)
    {
        Context = context;
        HealthBefore = healthBefore;
        AppliedPushesBefore = appliedPushesBefore;
        Outer = outer;
    }

    internal HitContext Context { get; }
    internal float HealthBefore { get; }
    internal int AppliedPushesBefore { get; }
    internal PayloadScope? Outer { get; }
    internal List<CombatResult> Results { get; } = new();

    internal void Collect(CombatResult? result)
    {
        if (result == null) { return; }
        (Outer ?? this).Results.Add(result);
    }
}

/// <summary>
/// Turns what happened to one <see cref="CombatantComponent"/> into published impact reports.
/// </summary>
internal sealed class ImpactReporter
{
    private readonly CombatantComponent _owner;
    private PayloadScope? _open;

    internal ImpactReporter(CombatantComponent owner)
    {
        _owner = owner;
    }

    internal PayloadScope BeginPayload(HitContext context)
    {
        var scope = new PayloadScope(
            context, _owner.Health?.CurrentHealth ?? 0f, AppliedPushes(LogOf())?.Count() ?? 0, _open);
        _open ??= scope;
        return scope;
    }

    internal void EndPayload(PayloadScope scope)
    {
        if (scope.Outer != null) { return; }
        _open = null;

        if (scope.Context.Kind != DamageKind.Direct) { return; }
        var health = _owner.Health;
        if (health == null) { return; }

        var profile = ProfileOrWarn();
        if (profile == null) { return; }

        var victim = ResolveVictim();
        var context = scope.Context;
        var direction = (context.ImpactDirection ?? context.HitDirection).Normalized();
        var inputs = new PayloadHitInputs(
            victim,
            ReferenceEquals(context.Attacker, victim) ? null : context.Attacker,
            context.Source,
            PointOf(victim),
            direction,
            scope.HealthBefore,
            health.CurrentHealth,
            health.MaxHealth,
            health.IsIndestructible,
            health.IsDead && scope.HealthBefore > 0f,
            scope.Results.OfType<DamageResult>().ToList(),
            scope.Results.OfType<IForceCarrier>().Sum(c => c.Force),
            AppliedDeltaV(scope),
            CategoriesOf(victim),
            context.Continuous);

        var report = HitReportBuilder.FromPayload(profile, inputs);
        if (report != null) { _owner.PublishReport(report); }
    }

    /// <summary>Reports direct damage that reached health outside any payload: reactions, shatters, kill volumes.</summary>
    internal void OnDamaged(HealthChangeEventArgs args)
    {
        if (_open != null || args.Kind != DamageKind.Direct) { return; }

        var profile = ProfileOrWarn();
        if (profile == null) { return; }

        var victim = ResolveVictim();
        var attacker = args.Source is Node node && GodotObject.IsInstanceValid(node) && !ReferenceEquals(node, victim) ? node : null;
        var report = HitReportBuilder.FromHealthChange(profile, args, victim, attacker, PointOf(victim), CategoriesOf(victim));
        if (report != null) { _owner.PublishReport(report); }
    }

    /// <summary>Reports this body striking a surface; the caller has already applied the damage.</summary>
    internal void ReportImpact(in ImpactInfo info, object source, float healthBefore, float requestedDamage)
    {
        var health = _owner.Health;
        if (health == null) { return; }

        var profile = ProfileOrWarn();
        if (profile == null) { return; }

        var victim = ResolveVictim();
        var sourceNode = source is Node node && GodotObject.IsInstanceValid(node) ? node : null;
        var report = HitReportBuilder.FromImpact(profile, new ImpactHitInputs(
            victim, sourceNode, PointOf(victim), info.Normal, info.SpeedAlongNormal, healthBefore,
            health.CurrentHealth, health.MaxHealth, health.IsDead && healthBefore > 0f, requestedDamage,
            CategoriesOf(victim)));
        if (report != null) { _owner.PublishReport(report); }
    }

    private ImpactWeightProfile? ProfileOrWarn()
    {
        var profile = CombatFactoryDefaults.ImpactWeight;
        if (profile == null) { HitReportSeam.TryWarnMissingProfile(_owner); }
        return profile;
    }

    private CombatLog? LogOf() =>
        _owner.Blackboard != null && _owner.Blackboard.TryGet<CombatLog>(BBDataSig.CombatLog, out var log) ? log : null;

    private static IEnumerable<KnockbackAppliedResult>? AppliedPushes(CombatLog? log) => log?.GetEvents<KnockbackAppliedResult>();

    private float AppliedDeltaV(PayloadScope scope) =>
        AppliedPushes(LogOf())?.Skip(scope.AppliedPushesBefore).Sum(push => push.DeltaV) ?? 0f;

    private Node ResolveVictim() => _owner.OwnerNode ?? _owner.GetParent() ?? _owner;

    private static Vector3 PointOf(Node victim) =>
        victim is Node3D node3D && node3D.IsInsideTree() ? node3D.GlobalPosition : Vector3.Zero;

    private static IReadOnlyList<Category> CategoriesOf(Node victim)
    {
        var categories = victim.TryResolveIdentifiable()?.GetIdentity()?.Categories;
        return categories == null ? [] : categories.Where(c => c != null).ToList();
    }
}
