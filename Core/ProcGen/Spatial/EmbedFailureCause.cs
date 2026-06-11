namespace Jmodot.Core.ProcGen.Spatial;

/// <summary>
///     Typed cause for a failed embed, carried on <see cref="FloorEmbedResult" />. The pipeline maps
///     <c>(FailureCause, FailingNodeId)</c> onto <see cref="Graph.Violation" /> at publish — that
///     mapping site is the only place embed failures become violations. Re-roll semantics are
///     parity-conditional and live in the pipeline, not the embedder: <see cref="ClosureParity" />
///     against a provably parity-uniform pool is seed-independent (fail fast, no re-roll);
///     <see cref="SpaceTight" /> and <see cref="NoBinding" /> re-roll normally in all cases.
/// </summary>
public enum EmbedFailureCause
{
    /// <summary>A cycle provably cannot close on the integer grid for the assigned templates (mod-2 residue / geometric floor).</summary>
    ClosureParity,

    /// <summary>The search exhausted its repair budget on occupancy or envelope conflicts.</summary>
    SpaceTight,

    /// <summary>The search exhausted its repair budget with no geometrically compatible pose/port binding.</summary>
    NoBinding,
}
