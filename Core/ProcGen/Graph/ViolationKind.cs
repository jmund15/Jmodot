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

    /// <summary>Embedding failed closure-parity arithmetic (no pose assignment can close a cycle on the grid).</summary>
    EmbedClosureParity,

    /// <summary>Embedding ran out of envelope space or repair budget while placing footprints.</summary>
    EmbedSpaceTight,

    /// <summary>Embedding found no compatible port binding for an edge.</summary>
    EmbedNoBinding,

    /// <summary>
    ///     Generation completed but fewer branches were grown than BranchSpec.Count.Min requested.
    ///     Appended last to preserve the append-only ordinal contract (serialized .tres ints must
    ///     never shift meaning).
    /// </summary>
    BranchesUnfilled,
}
