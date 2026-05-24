namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Directionality of a graph edge. A plain CLR enum (never .tres-authored) so the
///     graph kernel stays pure-CLR and dodges the Godot type-load allocation class.
/// </summary>
public enum EdgeTraversal
{
    /// <summary>Edge is traversable in both directions (default).</summary>
    Bidirectional = 0,

    /// <summary>Edge is traversable only From → To.</summary>
    SourceToTarget,
}
