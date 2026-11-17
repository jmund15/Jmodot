namespace Jmodot.Implementation.ProcGen;

using System;
using Godot;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;
using Jmodot.Core.Shared;

/// <summary>
///     The one place an <see cref="EmbedFailureCause" /> becomes a <see cref="Violation" />. The floor pipeline
///     and the topology generator both file embed failures through it, so a cause reads the same wherever it ends
///     an attempt.
/// </summary>
internal static class EmbedFailureMapping
{
    /// <summary>
    ///     The fatal violation for an embed failure. <paramref name="failingNodeId" /> names the node the embed
    ///     could not place, or null when the failure is not tied to one node.
    /// </summary>
    internal static Violation ToViolation(EmbedFailureCause cause, StringName? failingNodeId)
    {
        ViolationKind kind = cause switch
        {
            EmbedFailureCause.ClosureParity => ViolationKind.EmbedClosureParity,
            EmbedFailureCause.SpaceTight => ViolationKind.EmbedSpaceTight,
            EmbedFailureCause.NoBinding => ViolationKind.EmbedNoBinding,
            _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, "Unknown embed failure cause."),
        };

        string at = failingNodeId == null ? string.Empty : $" at node '{failingNodeId}'";
        return new Violation(kind, Severity.Fatal, $"Floor embedding failed ({cause}){at}.");
    }
}
