namespace Jmodot.Implementation.AI.Squad;

using BB;
using Core.AI.Squad;
using Core.Health;

/// <summary>
/// Pure aggregation of live <see cref="SquadRoster"/> state into an immutable <see cref="SquadSnapshot"/>.
/// Walks the roster's member graphs for <c>BBDataSig.HealthComponent</c> to compute the mean health
/// fraction; zero members OR zero resolvable health yields the neutral 0.5 default.
/// </summary>
public static class SquadSnapshotBuilder
{
    public static SquadSnapshot Build(SquadRoster roster, SquadDirectiveDefinition? currentDirective, float timeSinceChangeSeconds)
    {
        var graphs = roster.MemberGraphs;

        float averageHealthFraction = 0.5f;
        float sum = 0f;
        int resolved = 0;
        for (int i = 0; i < graphs.Count; i++)
        {
            if (graphs[i].Local.TryGet<IHealth>(BBDataSig.HealthComponent, out var health)
                && health != null && health.MaxHealth > 0f)
            {
                sum += health.CurrentHealth / health.MaxHealth;
                resolved++;
            }
        }

        if (resolved > 0)
        {
            averageHealthFraction = sum / resolved;
        }

        return new SquadSnapshot(
            MemberCount: roster.Members.Count,
            PeakMemberCount: roster.PeakMemberCount,
            AverageHealthFraction: averageHealthFraction,
            Leader: roster.Leader,
            CurrentDirective: currentDirective,
            TimeSinceDirectiveChangeSeconds: timeSinceChangeSeconds);
    }
}
