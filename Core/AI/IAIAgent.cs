namespace Jmodot.Core.AI;

using BB;
using Health;
using Shared;
using Stats;
using Jmodot.Implementation.AI.Affinities;

/// <summary>
/// Contract for AI agent components. Provides unified access to AI subsystems
/// (Blackboard, Affinities, Stats, Health) for consistent agent configuration.
/// </summary>
public interface IAIAgent : IGodotNodeInterface
{
    /// <summary>
    /// The agent's blackboard for data sharing between AI systems.
    /// </summary>
    IBlackboard Blackboard { get; }

    /// <summary>
    /// The agent's personality/affinity component. May be null if not configured.
    /// </summary>
    AIAffinitiesComponent? Affinities { get; }

    /// <summary>
    /// The agent's stat provider. May be null if not configured.
    /// </summary>
    IStatProvider? Stats { get; }

    /// <summary>
    /// The agent's health component. May be null if not configured.
    /// </summary>
    IHealth? Health { get; }

    /// <summary>
    /// Called after all components are discovered and wired to the blackboard.
    /// Automatically called in _Ready(), but can be called manually for testing.
    /// </summary>
    void Initialize();
}
