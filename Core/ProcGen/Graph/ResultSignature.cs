namespace Jmodot.Core.ProcGen.Graph;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///     Full-determinism oracle for a <see cref="GraphGenerationResult" /> (design §4/§9): composes
///     the topology signature (<see cref="GraphSignature.Of" />) with the per-node spatial layout.
///     <see cref="Of" /> is a pure function of the result's CONTENT — byte-identical for any two
///     results with the same topology + layout regardless of construction order or CLR identity.
///     <para>
///     The <see cref="GraphGenerationResult.Layout" /> dictionary is reference-keyed (hash-order
///     enumeration), so its records are ALWAYS ordinal-sorted before joining — never emitted in raw
///     iteration order, which would differ across CLR instances and defeat determinism.
///     </para>
/// </summary>
public static class ResultSignature
{
    /// <summary>Produces the canonical content signature of <paramref name="r" />. Stable, non-throwing on a fatal (null-graph) result.</summary>
    public static string Of(GraphGenerationResult r)
    {
        string topo = r.Graph is null ? "<null-graph>" : GraphSignature.Of(r.Graph);

        // Share GraphSignature's delimiters (internal const) — never re-declare literals that could drift.
        var layoutRecords = r.Layout
            .Select(kv => string.Join(
                GraphSignature.FieldSep,
                kv.Key.Id.ToString(),
                kv.Key.Template.TemplateId.ToString(), // kernel member — no boundary-violating downcast
                Quantize(kv.Value.Transform.Origin),
                QuantizeAabb(kv.Value.Bounds)))
            .OrderBy(s => s, StringComparer.Ordinal); // canonical order — NEVER raw dict iteration

        return topo + GraphSignature.SectionSep + string.Join(GraphSignature.RecordSep, layoutRecords);
    }

    // Grid coords are integer-valued floats by contract; round per axis so float noise can't fork the signature.
    private static string Quantize(Vector3 v)
        => $"{Mathf.RoundToInt(v.X)},{Mathf.RoundToInt(v.Y)},{Mathf.RoundToInt(v.Z)}";

    private static string QuantizeAabb(Aabb a)
        => $"{Quantize(a.Position)};{Quantize(a.Size)}";
}
