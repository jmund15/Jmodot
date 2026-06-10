namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     The class of topology constraint a <see cref="Violation" /> reports against a generation
///     result. Kept engine-side and PP-vocabulary-free (BOUNDARY invariant).
/// </summary>
public enum ViolationKind
{
    /// <summary>The spine could not be laid within the configured length / budget.</summary>
    SpineInfeasible = 0,

    /// <summary>Generation completed but the node budget was left under-filled.</summary>
    BudgetUnfilled,

    /// <summary>A pinned placement could not be satisfied (no admissible slot at its anchor).</summary>
    PinUnsatisfiable,

    /// <summary>Generation completed but fewer guaranteed alternate routes were embedded than required.</summary>
    AlternateRoutesUnfilled,
}
