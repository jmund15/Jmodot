namespace Jmodot.Implementation.AI.Squad;

using Godot;
using BB;
using Core.AI.Squad;
using Core.Shared.Attributes;

/// <summary>
/// Squad-stack node that selects the active directive by walking an ordered <see cref="SquadPolicy"/>
/// chain and publishing the first non-null result onto the squad blackboard graph under
/// <c>BBDataSig.SquadDirective</c>. Publication is gated by directive value-equality (no republish of
/// an equivalent directive) and a minimum hold time (flap protection). Contains ZERO fallback policy
/// logic — an all-abstain chain simply retains the current directive.
/// </summary>
[GlobalClass]
public partial class SquadDirectiveBrain : Node
{
    [Export, RequiredExport] private SquadRoster _roster = null!;
    [Export] private Godot.Collections.Array<SquadPolicy> _policies = new();

    [ExportGroup("Evaluation")]
    [Export(PropertyHint.Range, "0.05,10.0,0.05")] private float _evaluateIntervalSeconds = 0.5f;
    [Export(PropertyHint.Range, "0.0,30.0,0.1")] private float _minDirectiveHoldSeconds = 1.0f;

    private float _intervalAccumulator;
    private float _timeSinceDirectiveChangeSeconds;

    public SquadDirectiveDefinition? CurrentDirective { get; private set; }

    public override void _Ready() => this.ValidateRequiredExports();

    public override void _PhysicsProcess(double delta) => Tick((float)delta);

    /// <summary>Accumulates elapsed time; runs <see cref="EvaluateNow"/> once the evaluate interval elapses.</summary>
    public void Tick(float deltaSeconds)
    {
        _timeSinceDirectiveChangeSeconds += deltaSeconds;
        _intervalAccumulator += deltaSeconds;
        if (_intervalAccumulator >= _evaluateIntervalSeconds)
        {
            _intervalAccumulator = 0f;
            EvaluateNow();
        }
    }

    /// <summary>Builds a snapshot, resolves the first non-null policy, and publishes on-change (hold-gated).</summary>
    public void EvaluateNow()
    {
        var graph = _roster.SquadGraph;
        if (graph == null)
        {
            // Disbanded roster — never re-create the graph, never publish into a zombie scope.
            return;
        }

        if (_policies == null)
        {
            return;
        }

        var snapshot = SquadSnapshotBuilder.Build(_roster, CurrentDirective, _timeSinceDirectiveChangeSeconds);

        SquadDirectiveDefinition? selected = null;
        foreach (var policy in _policies)
        {
            if (policy == null)
            {
                continue;
            }

            var candidate = policy.Evaluate(in snapshot);
            if (candidate != null)
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null)
        {
            // All policies abstained — retain the current directive.
            return;
        }

        // Value-equal to the current directive (Category equality) → not a change.
        if (CurrentDirective != null && CurrentDirective.Equals(selected))
        {
            return;
        }

        // Replacing an existing directive requires the min-hold to have elapsed (>= counts as elapsed).
        if (CurrentDirective != null && _timeSinceDirectiveChangeSeconds < _minDirectiveHoldSeconds)
        {
            return;
        }

        CurrentDirective = selected;
        _timeSinceDirectiveChangeSeconds = 0f;
        graph.Local.Set(BBDataSig.SquadDirective, selected);
    }
}
