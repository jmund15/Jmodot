namespace Jmodot.Implementation.ProcGen;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.ProcGen;
using Jmodot.Core.ProcGen.Graph;
using Jmodot.Core.ProcGen.Spatial;
using Jmodot.Implementation.ProcGen.Graph;
using Jmodot.Implementation.ProcGen.Spatial;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
///     The single Jmodot-side floor entry point (design-se §1) and the ONE re-roll owner: per
///     attempt, <c>SeedManager.DeriveChild(seedRoot, "floor", attempt)</c> feeds the internal
///     single-attempt topology generator; the finished topology passes the pre-embed gates; the
///     embedder runs; embed failure re-rolls topology with the next attempt index. The embedder is
///     deterministic, so re-running it on identical topology cannot change anything — the outer
///     topology re-roll is the only retry that can.
///     <para>
///         Fail-fast exceptions to the re-roll rule (seed-independent failures never consume the
///         budget): config errors throw at entry; <see cref="ViolationKind.PinUnsatisfiable" />
///         returns immediately; <see cref="EmbedFailureCause.ClosureParity" /> against a
///         parity-UNIFORM pool returns immediately (every re-roll re-draws templates from the same
///         parity class — design-se §4's parity-conditional semantics). This is also the single
///         site where <see cref="EmbedFailureCause" /> becomes a <see cref="Violation" />.
///     </para>
/// </summary>
public static class FloorPipeline
{
    public static FloorGenerationResult Generate(
        ISkeletonConfig config,
        GeometryEnvelope envelope,
        int seedRoot,
        FloorPipelineSettings? settings = null,
        IFloorEmbedder? embedder = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(envelope);

        settings ??= new FloorPipelineSettings { Embedder = new EmbedderSettings() };
        settings.ValidateRequiredExports();
        if (settings.MaxFloorAttempts <= 0)
        {
            throw new ResourceConfigurationException(
                $"FloorPipelineSettings.MaxFloorAttempts must be positive; got {settings.MaxFloorAttempts}.", settings);
        }

        envelope.Validate();
        embedder ??= new GridFloorEmbedder();

        bool parityUniform = PoolIsParityUniform(config.TemplatePool);

        IReadOnlyList<Violation> lastViolations = new List<Violation>
        {
            new(ViolationKind.SpineInfeasible, Severity.Fatal, "No floor attempt produced a topology."),
        };

        for (int attempt = 0; attempt < settings.MaxFloorAttempts; attempt++)
        {
            int floorSeed = SeedManager.DeriveChild(seedRoot, "floor", attempt);
            var stage1 = GraphGenerator.GenerateSingle(config, floorSeed);
            if (!stage1.Succeeded)
            {
                if (stage1.Violations.Any(v => v.Reason == ViolationKind.PinUnsatisfiable))
                {
                    return FloorGenerationResult.Failure(attempt + 1, stage1.Violations);
                }

                lastViolations = stage1.Violations;
                continue;
            }

            IFloorGraph topology = stage1.Graph!;
            if (!PreEmbedGates.Check(topology, envelope, out EmbedFailureCause gateCause, out StringName? gateNode))
            {
                lastViolations = Append(stage1.Violations, MapEmbedFailure(gateCause, gateNode));
                continue;
            }

            FloorEmbedResult embed = embedder.Embed(topology, envelope, settings.Embedder);
            if (!embed.Succeeded)
            {
                EmbedFailureCause cause = embed.FailureCause!.Value;
                lastViolations = Append(stage1.Violations, MapEmbedFailure(cause, embed.FailingNodeId));
                if (cause == EmbedFailureCause.ClosureParity && parityUniform)
                {
                    return FloorGenerationResult.Failure(attempt + 1, lastViolations);
                }

                continue;
            }

            FloorGraph published = GraphRebuilder.Rebuild(topology, embed.Doorways);
            return FloorGenerationResult.Success(published, embed.Layout, embed.Doorways, attempt + 1, stage1.Violations);
        }

        return FloorGenerationResult.Failure(settings.MaxFloorAttempts, lastViolations);
    }

    // One parity class (or none) means ClosureParity is seed-independent: re-drawing templates can
    // never change a cycle's joint parity, so retrying is provably futile.
    private static bool PoolIsParityUniform(IReadOnlyList<INodeTemplate> templatePool)
    {
        var descriptors = new HashSet<string>(StringComparer.Ordinal);
        foreach (INodeTemplate template in templatePool)
        {
            if (template is not ISpatialNodeTemplate spatial)
            {
                continue;
            }

            var spatialPorts = template.Ports.OfType<ISpatialPort>().ToList();
            descriptors.Add(SpatialParity.DescriptorOf(spatial.FootprintCells, spatialPorts));
        }

        return descriptors.Count <= 1;
    }

    private static Violation MapEmbedFailure(EmbedFailureCause cause, StringName? failingNodeId)
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

    private static IReadOnlyList<Violation> Append(IReadOnlyList<Violation> warnings, Violation violation)
    {
        var combined = new List<Violation>(warnings.Count + 1);
        combined.AddRange(warnings);
        combined.Add(violation);
        return combined;
    }
}
