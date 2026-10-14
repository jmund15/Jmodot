namespace Jmodot.Implementation.Combat;

using System.Collections.Generic;
using System.Linq;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Health;
using Core.Identification;
using Shared;

/// <summary>
/// One hit being applied: the victim's health when it began and every result its effects produced.
/// A scope opened inside another one collects into the outer scope, which alone publishes.
/// </summary>
internal sealed class PayloadScope
{
    internal PayloadScope(HitContext context, float healthBefore, PayloadScope? outer)
    {
        Context = context;
        HealthBefore = healthBefore;
        Outer = outer;
    }

    internal HitContext Context { get; }
    internal float HealthBefore { get; }
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
        var scope = new PayloadScope(context, _owner.Health?.CurrentHealth ?? 0f, _open);
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

        var profile = CombatFactoryDefaults.ImpactWeight;
        if (profile == null)
        {
            HitReportSeam.TryWarnMissingProfile(_owner);
            return;
        }

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
            0f,
            CategoriesOf(victim),
            context.Continuous);

        var report = HitReportBuilder.FromPayload(profile, inputs);
        if (report != null) { _owner.PublishReport(report); }
    }

    private Node ResolveVictim() => _owner.OwnerNode ?? _owner.GetParent() ?? _owner;

    private static Vector3 PointOf(Node victim) =>
        victim is Node3D node3D && node3D.IsInsideTree() ? node3D.GlobalPosition : Vector3.Zero;

    private static IReadOnlyList<Category> CategoriesOf(Node victim)
    {
        var categories = victim.TryResolveIdentifiable()?.GetIdentity()?.Categories;
        return categories == null ? [] : categories.Where(c => c != null).ToList();
    }
}
