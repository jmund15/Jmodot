namespace Jmodot.Core.ProcGen.Graph;

/// <summary>
///     Authored rules for the corridors that close a loop: <paramref name="Policy" /> selects which path
///     families may close it, and <paramref name="MaxLengthCells" /> bounds how long any synthesized path may be.
///     <c>default</c> is <see cref="ConnectorPolicy.Closable" /> bounded only by the envelope.
/// </summary>
public readonly record struct ConnectorRules(ConnectorPolicy Policy = ConnectorPolicy.Closable, int MaxLengthCells = 0);
