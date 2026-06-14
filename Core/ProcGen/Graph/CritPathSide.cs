namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Which side of the critical path the crit-path membership rule admits. <see cref="Unset" /> is
///     the invalid-at-zero sentinel (mirrors the <see cref="EdgeProvenanceKind.Unset" /> convention):
///     a rule left on <see cref="Unset" /> is a misconfiguration — admissibility rejects it loudly
///     rather than silently filtering, so adding the rule without choosing a side fails fast instead
///     of producing a surprise off-path filter.
/// </summary>
public enum CritPathSide
{
    Unset = 0,
    OnCriticalPath,
    OffCriticalPath,
}
