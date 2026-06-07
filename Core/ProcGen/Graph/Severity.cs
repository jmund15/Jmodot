namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Severity of a <see cref="Violation" />. A result is <c>Succeeded == false</c> iff it carries
///     at least one <see cref="Fatal" /> violation; <see cref="Warning" />s are non-fatal advisories.
/// </summary>
public enum Severity
{
    /// <summary>A fatal violation — the generation result is unusable (e.g. no graph produced).</summary>
    Fatal = 0,

    /// <summary>A non-fatal advisory — the result is usable but suboptimal (e.g. budget under-filled).</summary>
    Warning,
}
