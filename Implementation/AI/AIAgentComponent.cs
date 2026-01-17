namespace Jmodot.Implementation.AI;

using Jmodot.Core.AI;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Health;
using Jmodot.Core.Stats;
using Jmodot.Core.Shared;
using Affinities;
using BB;
using Shared;

/// <summary>
/// Auto-discovers AI subsystem components and wires them to the blackboard.
/// Add as a child of your AI entity root node to eliminate manual wiring boilerplate.
///
/// <example>
/// Scene structure:
/// <code>
/// EnemyWizard (CharacterBody3D)
///   ├── AIAgentComponent      ← Just add this!
///   ├── AIAffinitiesComponent
///   ├── StatController
///   ├── HealthComponent
///   └── HSM
/// </code>
/// </example>
/// </summary>
[GlobalClass]
public partial class AIAgentComponent : Node, IAIAgent
{
    /// <summary>
    /// The agent's blackboard. Always initialized - never null.
    /// </summary>
    [Export]
    public Blackboard Blackboard { get; private set; } = new();

    /// <summary>
    /// Auto-discovered affinities component. Null if not found.
    /// </summary>
    public AIAffinitiesComponent? Affinities { get; private set; }

    /// <summary>
    /// Auto-discovered stat provider. Null if not found.
    /// </summary>
    public IStatProvider? Stats { get; private set; }

    /// <summary>
    /// Auto-discovered health component. Null if not found.
    /// </summary>
    public IHealth? Health { get; private set; }

    // Interface implementation
    IBlackboard IAIAgent.Blackboard => Blackboard;
    Node IGodotNodeInterface.GetUnderlyingNode() => this;

    public override void _Ready()
    {
        // Add blackboard as child so it's in the scene tree
        if (Blackboard.GetParent() == null)
        {
            AddChild(Blackboard);
        }

        Initialize();
    }

    /// <summary>
    /// Discovers components from parent/siblings and wires them to the blackboard.
    /// Automatically called in _Ready(), but exposed for testing.
    /// </summary>
    public void Initialize()
    {
        var parent = GetParent();
        if (parent == null)
        {
            JmoLogger.Warning(this, "AIAgentComponent has no parent - cannot discover components.");
            return;
        }

        // Discover components from parent and its children (our siblings)
        DiscoverComponents(parent);

        // Wire to blackboard
        WireToBlackboard(parent);

        LogDiscoveryResults(parent);
    }

    private void DiscoverComponents(Node parent)
    {
        // Auto-discover from parent's children (our siblings)
        parent.TryGetFirstChildOfType(out AIAffinitiesComponent? affinities);
        Affinities = affinities;

        parent.TryGetFirstChildOfInterface(out IStatProvider? stats);
        Stats = stats;

        parent.TryGetFirstChildOfInterface(out IHealth? health);
        Health = health;
    }

    private void WireToBlackboard(Node parent)
    {
        // Always wire the agent (parent node)
        Blackboard.Set(BBDataSig.Agent, parent);

        // Conditionally wire discovered components
        if (Affinities != null)
        {
            Blackboard.Set(BBDataSig.Affinities, Affinities);
        }

        if (Stats != null)
        {
            Blackboard.Set(BBDataSig.Stats, Stats);
        }

        if (Health != null)
        {
            Blackboard.Set(BBDataSig.HealthComponent, Health);
        }
    }

    private void LogDiscoveryResults(Node parent)
    {
        JmoLogger.Info(this,
            $"AIAgentComponent initialized for {parent.Name}. " +
            $"Affinities={Affinities != null}, " +
            $"Stats={Stats != null}, " +
            $"Health={Health != null}");
    }
}
