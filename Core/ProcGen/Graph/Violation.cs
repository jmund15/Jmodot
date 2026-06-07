namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     A single topology-feasibility problem reported by the generator. A data-only carrier — no
///     behavior. <see cref="Detail" /> is authored entirely by the engine (no PushinPotions
///     vocabulary — BOUNDARY invariant): it describes the topology problem, never a PP concept.
/// </summary>
public readonly struct Violation
{
    public Violation(ViolationKind reason, Severity severity, string detail)
    {
        this.Reason = reason;
        this.Severity = severity;
        this.Detail = detail;
    }

    /// <summary>The class of constraint violated.</summary>
    public ViolationKind Reason { get; }

    /// <summary>Whether this violation is fatal (result unusable) or a non-fatal advisory.</summary>
    public Severity Severity { get; }

    /// <summary>Engine-authored, PP-free description of the problem.</summary>
    public string Detail { get; }
}
