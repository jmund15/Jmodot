namespace Jmodot.Implementation.AI.Navigation.Targets;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Perception;
using Core.Identification;
using Godot;
using Implementation.AI.Perception;
using Implementation.Shared;

/// <summary>Which sensed memories a category target treats as eligible.</summary>
public enum TargetEligibility
{
    /// <summary>Not chosen. A category target with this value is a configuration fault.</summary>
    Unset = 0,

    /// <summary>Every active memory, including ones decaying after their sensor lost them.</summary>
    RememberedTargets = 1,

    /// <summary>Only memories a sensor is still reporting right now.</summary>
    VisibleNow = 2,
}

/// <summary>
/// Resolves every live memory the agent's perception holds in one category and hands them to a
/// policy, which decides whether the result is one member or the whole set.
/// </summary>
[GlobalClass, Tool]
public partial class CategoryTarget3D : MovementTarget3D
{
    /// <summary>
    /// The sensed category this target reads. Required: with no category the target resolves
    /// Unavailable and says so in the Inspector.
    /// </summary>
    [Export] public Category? Category { get; private set; }

    /// <summary>
    /// How the eligible members become a sample: nearest one, or the whole set. Required.
    /// </summary>
    [Export] public TargetPolicy3D? Policy { get; private set; }

    /// <summary>
    /// Remembered targets include memories decaying after loss; visible-now only what a sensor
    /// still reports. Unset is a fault, never an implicit default.
    /// </summary>
    [Export] public TargetEligibility Eligibility { get; private set; } = TargetEligibility.Unset;

    public override TargetSampleGeometry Geometry => Policy?.Geometry ?? TargetSampleGeometry.Member;

    public override void Resolve(Vector3 agentPosition, IBlackboard bb,
        AIPerceptionManager3D? memory, TargetSample sample)
    {
        sample.Clear();

        var faults = GetResourceConfigurationWarnings();
        if (faults.Length > 0)
        {
            sample.State = TargetSampleState.Unavailable;
            if (sample.TryBeginLog(TargetLogKind.ConfigError))
            {
                JmoLogger.Error(this, $"CategoryTarget3D '{ResourceName}' cannot resolve: {faults[0]}");
            }
            return;
        }

        if (memory == null)
        {
            sample.State = TargetSampleState.Unavailable;
            if (sample.TryBeginLog(TargetLogKind.ConfigError))
            {
                JmoLogger.Error(this,
                    $"CategoryTarget3D '{ResourceName}' cannot resolve: {MissingManager}");
            }
            return;
        }

        var members = new List<TargetMember>();
        foreach (var info in memory.GetSensedByCategory(Category!))
        {
            if (!info.IsLiveTarget()) { continue; }
            if (Eligibility == TargetEligibility.VisibleNow && !info.IsSensing) { continue; }
            members.Add(TargetMember.FromMemory(info, agentPosition));
        }

        if (members.Count == 0)
        {
            sample.State = TargetSampleState.Empty;
            return;
        }

        sample.Adopt(members, Policy!.Geometry);
        Policy.Apply(sample);
    }

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Category == null)
        {
            warnings.Add("Category is empty. Choose the sensed category this target reads.");
        }

        if (Eligibility == TargetEligibility.Unset)
        {
            warnings.Add("Eligibility is Unset. Choose RememberedTargets to include memories decaying after " +
                         "loss, or VisibleNow to use only what a sensor currently reports.");
        }

        warnings.AddRange(ConfigWarnings.ResourceSlotWarnings("Policy", Policy, required: true));
        return warnings.ToArray();
    }

    public override string[] GetAgentWarnings(Node agent)
    {
        var warning = ConfigWarnings.RequireEntitySibling<AIPerceptionManager3D>(agent, MissingManager);
        return warning == null ? Array.Empty<string>() : new[] { warning };
    }

    private const string MissingManager =
        "Category targeting needs an AIPerceptionManager3D on this entity.";

    #region Test Helpers
#if TOOLS
    internal void SetCategoryForTest(Category? category) => Category = category;
    internal void SetPolicyForTest(TargetPolicy3D? policy) => Policy = policy;
    internal void SetEligibilityForTest(TargetEligibility eligibility) => Eligibility = eligibility;
#endif
    #endregion
}
