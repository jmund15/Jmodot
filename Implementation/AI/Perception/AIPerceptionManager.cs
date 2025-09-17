namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.Perception;
using Core.Identification;
using Core.Shared;
using Godot.Collections;

/// <summary>
///     The central hub of the perception system, acting as the AI's short-term memory. It collects
///     stateless `Percept` events from all registered sensors and manages a stateful collection of
///     `PerceptionInfo` records. It provides the AI Brain with a clean, processed, and efficiently
///     queryable view of the game world.
/// </summary>
[GlobalClass]
public partial class AIPerceptionManager : Node, IGodotNodeInterface
{
    private readonly System.Collections.Generic.Dictionary<Category, HashSet<PerceptionInfo>> _memoryByCategory = new();

    private readonly System.Collections.Generic.Dictionary<Node3D, PerceptionInfo> _memoryByTarget = new();

    /// <summary>A list of all Nodes in the scene that implement the IAISensor interface. Assign in the Godot Editor.</summary>
    [Export] private Array<Node> _sensors = new();

    public event EventHandler<PerceptionInfo> MemoryAddedEventHandler;
    public event EventHandler<PerceptionInfo> MemoryUpdatedEventHandler;
    public event EventHandler<PerceptionInfo> MemoryForgottenEventHandler;

    public override void _Ready()
    {
        foreach (var node in this._sensors)
            if (node is IAISensor sensor)
                sensor.PerceptUpdated += this.OnPerceptUpdated;
    }

    private void OnPerceptUpdated(object sender, PerceptEventArgs args)
    {
        var percept = args.Percept;
        if (percept.Target == null || percept.Identity == null) return;

        if (this._memoryByTarget.TryGetValue(percept.Target, out var info))
        {
            this.RemoveFromCategoryCache(info);
            info.Update(percept);
            this.AddToCategoryCache(info);
            this.MemoryUpdatedEventHandler?.Invoke(this, info);
        }
        else
        {
            var newInfo = new PerceptionInfo(percept);
            this._memoryByTarget.Add(percept.Target, newInfo);
            this.AddToCategoryCache(newInfo);
            this.MemoryAddedEventHandler?.Invoke(this, newInfo);
        }
    }

    private void AddToCategoryCache(PerceptionInfo info)
    {
        if (info.Identity?.Categories == null) return;
        foreach (var category in info.Identity.Categories)
        {
            if (category == null) continue;
            if (!this._memoryByCategory.TryGetValue(category, out var set))
            {
                set = new HashSet<PerceptionInfo>();
                this._memoryByCategory.Add(category, set);
            }

            set.Add(info);
        }
    }

    private void RemoveFromCategoryCache(PerceptionInfo info)
    {
        if (info.Identity?.Categories == null) return;
        foreach (var category in info.Identity.Categories)
        {
            if (category == null) continue;
            if (this._memoryByCategory.TryGetValue(category, out var set)) set.Remove(info);
        }
    }

    /// <summary>
    ///     This method should be connected to a Timer's timeout signal for periodic, performant cleanup of expired
    ///     memories.
    /// </summary>
    private void OnCleanupTimerTimeout()
    {
        var forgottenKeys = this._memoryByTarget.Where(kvp => !kvp.Value.IsActive).Select(kvp => kvp.Key).ToList();
        foreach (var key in forgottenKeys)
        {
            if (!this._memoryByTarget.TryGetValue(key, out var info)) continue;
            this.MemoryForgottenEventHandler?.Invoke(this, info);
            this.RemoveFromCategoryCache(info);
            this._memoryByTarget.Remove(key);
        }
    }

    #region Public API for AI Brain & Other Systems

    /// <summary>Tries to retrieve the memory record for a specific target.</summary>
    public bool TryGetMemoryOf(Node3D target, out PerceptionInfo info)
    {
        return this._memoryByTarget.TryGetValue(target, out info) && info.IsActive;
    }

    /// <summary>Returns an enumerable collection of all currently active memory records.</summary>
    public IEnumerable<PerceptionInfo> GetAllActiveMemories()
    {
        return this._memoryByTarget.Values.Where(info => info.IsActive);
    }

    /// <summary>
    ///     Finds the most prominent (highest confidence) active memory belonging to a specific category. This is a highly
    ///     performant query.
    /// </summary>
    public PerceptionInfo GetBestMemoryForCategory(Category category)
    {
        if (category == null || !this._memoryByCategory.TryGetValue(category, out var memorySet)) return null;

        PerceptionInfo bestMatch = null;
        var maxConfidence = -1.0f;

        foreach (var info in memorySet)
            if (info.IsActive && info.CurrentConfidence > maxConfidence)
            {
                maxConfidence = info.CurrentConfidence;
                bestMatch = info;
            }

        return bestMatch;
    }

    public IEnumerable<PerceptionInfo> GetSensedByCollLayer(int collLayer)
    {
        return this._memoryByTarget.Keys.OfType<CollisionObject3D>()
            .Where(obj => obj.GetCollisionLayerValue(collLayer))
            .Select(obj => this._memoryByTarget[obj]);
    }

    public Node GetInterfaceNode()
    {
        return this;
    }

    #endregion
}
