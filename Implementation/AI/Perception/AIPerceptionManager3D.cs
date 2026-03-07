namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.Perception;
using Core.Identification;
using Core.Shared;
using Godot.Collections;
using Implementation.AI.Perception.Strategies;

/// <summary>
///     The central hub of the perception system, acting as the AI's short-term memory. It collects
///     stateless `Percept` events from all registered sensors and manages a stateful collection of
///     `PerceptionInfo` records. It provides the AI Brain with a clean, processed, and efficiently
///     queryable view of the game world.
/// </summary>
[GlobalClass]
public partial class AIPerceptionManager3D : Node, IGodotNodeInterface
{
    private readonly System.Collections.Generic.Dictionary<Category, HashSet<Perception3DInfo>> _memoryByCategory = new();

    private readonly System.Collections.Generic.Dictionary<Node3D, Perception3DInfo> _memoryByTarget = new();

    private readonly System.Collections.Generic.Dictionary<Node3D, System.Collections.Generic.Dictionary<IAISensor3D, SensorContribution>> _sensorContributions = new();

    /// <summary>A list of all Nodes in the scene that implement the IAISensor interface. Assign in the Godot Editor.</summary>
    [Export] private Array<Node> _sensors = new();

    /// <summary>How multiple sensor contributions are combined into a single fused confidence value.</summary>
    [Export] private FusionMode _fusionMode = FusionMode.Additive;

    public event EventHandler<Perception3DInfo> MemoryAddedEventHandler;
    public event EventHandler<Perception3DInfo> MemoryUpdatedEventHandler;
    public event EventHandler<Perception3DInfo> MemoryForgottenEventHandler;

    public override void _Ready()
    {
        foreach (var node in this._sensors)
        {
            if (node is IAISensor3D sensor)
            {
                sensor.PerceptUpdated += this.OnPerceptUpdated;

                if (node is Node sensorNode)
                {
                    var capturedSensor = sensor;
                    sensorNode.TreeExiting += () =>
                    {
                        this.HandleSensorRemoved(capturedSensor);
                    };
                }
            }
        }
    }

    internal void HandleSensorRemoved(IAISensor3D sensor)
    {
        var targetsToRefuse = new List<(Node3D target, Identity identity)>();

        foreach (var kvp in this._sensorContributions)
        {
            if (!kvp.Value.TryGetValue(sensor, out var contrib))
            {
                continue;
            }

            contrib.SensingActive = false;
            contrib.ExitTime = Time.GetTicksMsec();

            if (this._memoryByTarget.TryGetValue(kvp.Key, out var info))
            {
                targetsToRefuse.Add((kvp.Key, info.Identity));
            }
        }

        foreach (var (target, identity) in targetsToRefuse)
        {
            this.RefuseAndPush(target, identity);
        }
    }

    private const float ConfidenceEpsilon = 0.001f;

    private void OnPerceptUpdated(IAISensor3D sensor, Percept3D percept)
    {
        if (percept.Target == null || percept.Identity == null)
        {
            return;
        }

        if (!this._sensorContributions.TryGetValue(percept.Target, out var contributions))
        {
            contributions = new System.Collections.Generic.Dictionary<IAISensor3D, SensorContribution>();
            this._sensorContributions.Add(percept.Target, contributions);
        }

        if (percept.Confidence > ConfidenceEpsilon)
        {
            if (!contributions.TryGetValue(sensor, out var contrib))
            {
                contrib = new SensorContribution();
                contributions.Add(sensor, contrib);
            }

            contrib.BaseConfidence = percept.Confidence;
            contrib.Position = percept.LastKnownPosition;
            contrib.Velocity = percept.LastKnownVelocity;
            contrib.DecayStrategy = percept.DecayStrategy;
            contrib.SensingActive = true;
            contrib.LastUpdateTime = percept.Timestamp;
        }
        else if (contributions.TryGetValue(sensor, out var contrib))
        {
            contrib.SensingActive = false;
            contrib.ExitTime = percept.Timestamp;
        }

        this.RefuseAndPush(percept.Target, percept.Identity);
    }

    private void RefuseAndPush(Node3D target, Identity identity)
    {
        if (!this._sensorContributions.TryGetValue(target, out var contributions))
        {
            return;
        }

        var deadKeys = new List<IAISensor3D>();
        foreach (var kvp in contributions)
        {
            if (!kvp.Value.IsAlive)
            {
                deadKeys.Add(kvp.Key);
            }
        }

        foreach (var key in deadKeys)
        {
            contributions.Remove(key);
        }

        if (contributions.Count == 0)
        {
            this._sensorContributions.Remove(target);

            if (this._memoryByTarget.TryGetValue(target, out var info))
            {
                this.RemoveFromCategoryCache(info);
                var exitPercept = new Percept3D(
                    target: target,
                    position: info.LastKnownPosition,
                    velocity: Vector3.Zero,
                    identity: identity,
                    confidence: 0f,
                    decayStrategy: info.Identity?.ResolvePerceptionDecay() ?? new LinearMemoryDecay()
                );
                info.Update(exitPercept);
                this.AddToCategoryCache(info);
                this.MemoryUpdatedEventHandler?.Invoke(this, info);
            }

            return;
        }

        var fusedConfidence = PerceptionFusion.FuseConfidence(contributions.Values, this._fusionMode);
        var best = PerceptionFusion.SelectBestContribution(contributions.Values);
        if (best == null) { return; }

        var fusedPercept = new Percept3D(
            target: target,
            position: best.Position,
            velocity: best.Velocity,
            identity: identity,
            confidence: fusedConfidence,
            decayStrategy: best.DecayStrategy
        );

        if (this._memoryByTarget.TryGetValue(target, out var existingInfo))
        {
            this.RemoveFromCategoryCache(existingInfo);
            existingInfo.Update(fusedPercept);
            this.AddToCategoryCache(existingInfo);
            this.MemoryUpdatedEventHandler?.Invoke(this, existingInfo);
        }
        else
        {
            var newInfo = new Perception3DInfo(fusedPercept);
            this._memoryByTarget.Add(target, newInfo);
            this.AddToCategoryCache(newInfo);
            this.MemoryAddedEventHandler?.Invoke(this, newInfo);
        }
    }

    private void AddToCategoryCache(Perception3DInfo info)
    {
        if (info.Identity?.Categories == null)
        {
            return;
        }

        foreach (var category in info.Identity.Categories)
        {
            if (category == null)
            {
                continue;
            }

            if (!this._memoryByCategory.TryGetValue(category, out var set))
            {
                set = new HashSet<Perception3DInfo>();
                this._memoryByCategory.Add(category, set);
            }

            set.Add(info);
        }
    }

    private void RemoveFromCategoryCache(Perception3DInfo info)
    {
        if (info.Identity?.Categories == null)
        {
            return;
        }

        foreach (var category in info.Identity.Categories)
        {
            if (category == null)
            {
                continue;
            }

            if (this._memoryByCategory.TryGetValue(category, out var set))
            {
                set.Remove(info);
            }
        }
    }

    /// <summary>
    ///     Connected to a Timer's timeout signal for periodic cleanup and re-fusion of decaying contributions.
    ///     Re-fuses all targets with active sensor contributions to provide smooth confidence updates.
    ///     Prunes fully dead targets from memory.
    /// </summary>
    private void OnCleanupTimerTimeout()
    {
        var targetsToRefuse = this._sensorContributions.Keys.ToList();
        foreach (var target in targetsToRefuse)
        {
            if (!this._memoryByTarget.TryGetValue(target, out var info))
            {
                continue;
            }

            this.RefuseAndPush(target, info.Identity);
        }

        var forgottenKeys = this._memoryByTarget
            .Where(kvp => !kvp.Value.IsActive && !this._sensorContributions.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Key).ToList();

        foreach (var key in forgottenKeys)
        {
            if (!this._memoryByTarget.TryGetValue(key, out var info))
            {
                continue;
            }

            this.MemoryForgottenEventHandler?.Invoke(this, info);
            this.RemoveFromCategoryCache(info);
            this._memoryByTarget.Remove(key);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetSensors(Array<Node> sensors) => _sensors = sensors;
    internal void SetFusionMode(FusionMode mode) => _fusionMode = mode;
#endif
    #endregion

    #region Public API for AI Brain & Other Systems

    /// <summary>Tries to retrieve the memory record for a specific target.</summary>
    public bool TryGetMemoryOf(Node3D target, out Perception3DInfo info)
    {
        return this._memoryByTarget.TryGetValue(target, out info) && info.IsActive;
    }

    /// <summary>Returns an enumerable collection of all currently active memory records.</summary>
    public IEnumerable<Perception3DInfo> GetAllActiveMemories()
    {
        return this._memoryByTarget.Values.Where(info => info.IsActive);
    }

    public IEnumerable<Perception3DInfo> GetSensedByCategory(Category category)
    {
        if (!this._memoryByCategory.TryGetValue(category, out var memorySet))
        {
            return [];
        }
        return memorySet.Where(info => info.IsActive);
    }

    /// <summary>
    ///     Finds the most prominent (highest confidence) active memory belonging to a specific category. This is a highly
    ///     performant query.
    /// </summary>
    public Perception3DInfo? GetBestMemoryForCategory(Category category)
    {
        var memorySet = GetSensedByCategory(category);

        Perception3DInfo bestMatch = null;
        var maxConfidence = -1.0f;

        foreach (var info in memorySet)
        {
            if (info.IsActive && info.CurrentConfidence > maxConfidence)
            {
                maxConfidence = info.CurrentConfidence;
                bestMatch = info;
            }
        }

        return bestMatch;
    }

    public IEnumerable<Perception3DInfo> GetSensedByCollLayer(int collLayer)
    {
        return this._memoryByTarget.Keys.OfType<CollisionObject3D>()
            .Where(obj => obj.GetCollisionLayerValue(collLayer))
            .Select(obj => this._memoryByTarget[obj]);
    }

    public Node GetUnderlyingNode()
    {
        return this;
    }

    #endregion
}
