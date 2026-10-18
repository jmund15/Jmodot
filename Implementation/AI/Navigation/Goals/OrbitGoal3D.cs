namespace Jmodot.Implementation.AI.Navigation.Goals;

using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.Navigation.Considerations;
using Core.Movement;
using Jmodot.Implementation.AI.Navigation.Targets;
using Jmodot.Implementation.Shared;
using Godot;

/// <summary>
/// Circles the target on a seeded ring and never ends itself: it fails when the target is lost and
/// keeps running while it holds. The ring radius is one draw per entity life, so two agents sharing
/// this goal spread evenly and stay on their own rings. End the step with a time limit or a later
/// branch.
/// </summary>
[GlobalClass, Tool]
public partial class OrbitGoal3D : SteeredGoal3D
{
    /// <summary>
    /// Ring radius around the target, in metres. One seeded draw per entity life varies it by
    /// RadiusSpread; it is not redrawn per tick.
    /// </summary>
    [Export(PropertyHint.Range, "1.0,30.0,0.5,suffix:m")] public float Radius { get; private set; } = 6f;

    /// <summary>How far either way the seeded ring may vary from Radius, in metres. 0 puts every agent on the same ring.</summary>
    [Export(PropertyHint.Range, "0.0,10.0,0.1,suffix:m")] public float RadiusSpread { get; private set; }

    /// <summary>Distance over which the pull back onto the ring ramps to full strength, in metres.</summary>
    [Export(PropertyHint.Range, "0.5,10.0,0.5,suffix:m")] public float RadialSoftness { get; private set; } = 2.5f;

    /// <summary>Seconds between direction reversals. 0 never reverses; Orbit still needs a BT or state to end it.</summary>
    [Export(PropertyHint.Range, "0.0,60.0,0.5,suffix:s")] public float FlipIntervalSeconds { get; private set; } = 7f;

    /// <summary>SeededFlip reverses on the interval; MatchTargetLateral follows the target's sideways motion.</summary>
    [Export] public OrbitSignSource SignSource { get; private set; } = OrbitSignSource.SeededFlip;

    /// <summary>Sideways target speed, in m/s, below which a mirroring orbit stops sidestepping.</summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.05,suffix:m/s")] public float MirrorDeadzone { get; private set; } = 0.3f;

    /// <summary>Overall target speed, in m/s, under which a mirroring orbit holds ground inside HoldBand.</summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.05,suffix:m/s")] public float HoldStandSpeed { get; private set; } = 0.3f;

    /// <summary>Distance from the ring, in metres, inside which a mirroring orbit facing a still target holds ground.</summary>
    [Export(PropertyHint.Range, "0.0,5.0,0.05,suffix:m")] public float HoldBand { get; private set; } = 0.25f;

    public override bool EndsItself => false;

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        var name = property["name"].AsString();
        var usage = (PropertyUsageFlags)property["usage"].AsInt64();
        bool hideMirror = name is nameof(MirrorDeadzone) or nameof(HoldStandSpeed) or nameof(HoldBand)
                          && SignSource != OrbitSignSource.MatchTargetLateral;
        bool hideFlip = name == nameof(FlipIntervalSeconds) && SignSource != OrbitSignSource.SeededFlip;
        if (hideMirror || hideFlip) { property["usage"] = (long)(usage & ~PropertyUsageFlags.Editor); }
    }

    /// <summary>Per-agent orbit state, including the activation-start values a re-enter restores.</summary>
    public sealed class OrbitRuntime : TargetRuntime
    {
        public float Radius;
        public float Sign = 1f;
        public float TimeToFlip;
        public bool Holding;
        public float ActivationSign = 1f;
        public float ActivationTimeToFlip;

        public void ResetActivation()
        {
            Sign = ActivationSign;
            TimeToFlip = ActivationTimeToFlip;
            Holding = false;
        }
    }

    public override AIConsiderationRuntime? CreateRuntime(IBlackboard? bb)
    {
        bool warned = false;
        float unit = EntityRngResolver.ResolveUnit(bb, SeedKinds.OrbitGoal, this, ref warned);

        var runtime = new OrbitRuntime
        {
            Radius = Radius + (2f * unit - 1f) * RadiusSpread,
            ActivationSign = unit < 0.5f ? 1f : -1f,
            ActivationTimeToFlip = FlipIntervalSeconds * (0.5f + 0.5f * unit),
        };
        runtime.ResetActivation();
        return runtime;
    }

    public override void OnEnter(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
        => (runtime as OrbitRuntime)?.ResetActivation();

    public override GoalStatus Advance(in MovementGoalContext ctx, AIConsiderationRuntime? runtime)
    {
        var sample = ResolveTarget(ctx, runtime);
        if (!HasUsableConfiguration)
        {
            if (sample.TryBeginLog(TargetLogKind.ConfigError))
            {
                JmoLogger.Error(this,
                    $"OrbitGoal3D '{ResourceName}' has an unusable numeric export; the goal fails " +
                    "instead of orbiting a ring it cannot trust.");
            }
            return GoalStatus.Failed;
        }
        if (sample.State != TargetSampleState.Resolved) { return GoalStatus.Failed; }
        if (runtime is not OrbitRuntime orbit) { return GoalStatus.Running; }

        var nearest = NearestMember(sample);
        var toTarget = sample.Point - ctx.AgentPosition;
        toTarget.Y = 0f;

        if (SignSource == OrbitSignSource.MatchTargetLateral)
        {
            if (toTarget.LengthSquared() > 0f)
            {
                orbit.Sign = OrbitScoring.MirrorSign(
                    nearest.Velocity, Vector3.Up.Cross(toTarget.Normalized()), MirrorDeadzone);
            }
        }
        else
        {
            (orbit.Sign, orbit.TimeToFlip) = OrbitScoring.Flip(
                orbit.Sign, orbit.TimeToFlip, FlipIntervalSeconds, ctx.Delta);
        }

        orbit.Holding = OrbitScoring.UpdateHold(orbit.Sign, nearest.Velocity,
            sample.NearestDistance - orbit.Radius, orbit.Holding, HoldBand, HoldStandSpeed);
        return GoalStatus.Running;
    }

    protected override Dictionary<Vector3, float> ScoreSample(DirectionSet3D directions,
        TargetSample sample, SteeringDecisionContext3D ctx, AIConsiderationRuntime? runtime)
    {
        if (!HasUsableConfiguration) { return ZeroScores(directions); }
        if (runtime is not OrbitRuntime orbit || orbit.Holding) { return ZeroScores(directions); }

        var toTarget = sample.Point - ctx.AgentPosition;
        toTarget.Y = 0f;
        var desired = OrbitScoring.Desired(toTarget, orbit.Radius, RadialSoftness, orbit.Sign);
        if (desired == Vector3.Zero) { return ZeroScores(directions); }
        return Considerations.DirectionScoring.ByAlignment(directions, desired);
    }

    public override GoalDistanceBand? DistanceBand
    {
        get
        {
            if (!float.IsFinite(Radius) || !(Radius > 0f)) { return null; }
            if (!float.IsFinite(RadiusSpread) || RadiusSpread < 0f || RadiusSpread >= Radius) { return null; }
            return new GoalDistanceBand(Radius - RadiusSpread, Radius + RadiusSpread,
                Target?.Geometry ?? TargetSampleGeometry.Member);
        }
    }

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>(base.GetResourceConfigurationWarnings());
        if (!float.IsFinite(Radius) || !(Radius > 0f))
        {
            warnings.Add($"Radius must be a finite distance greater than 0 (it is {Radius}).");
        }
        if (!float.IsFinite(RadiusSpread) || RadiusSpread < 0f || RadiusSpread >= Radius)
        {
            warnings.Add($"RadiusSpread must be at least 0 and less than Radius (it is {RadiusSpread}).");
        }
        if (!float.IsFinite(RadialSoftness) || !(RadialSoftness > 0f))
        {
            warnings.Add($"RadialSoftness must be greater than 0 (it is {RadialSoftness}).");
        }
        if (!float.IsFinite(FlipIntervalSeconds) || FlipIntervalSeconds < 0f)
        {
            warnings.Add("FlipIntervalSeconds must be a finite number of seconds; 0 means never " +
                         $"reverse (it is {FlipIntervalSeconds}).");
        }
        if (!IsNonNegative(MirrorDeadzone))
        {
            warnings.Add($"MirrorDeadzone must be a finite speed of 0 or more (it is {MirrorDeadzone}).");
        }
        if (!IsNonNegative(HoldStandSpeed))
        {
            warnings.Add($"HoldStandSpeed must be a finite speed of 0 or more (it is {HoldStandSpeed}).");
        }
        if (!IsNonNegative(HoldBand))
        {
            warnings.Add($"HoldBand must be a finite distance of 0 or more (it is {HoldBand}).");
        }
        if (!System.Enum.IsDefined(typeof(OrbitSignSource), SignSource))
        {
            warnings.Add("SignSource is not a known orbit mode. Choose SeededFlip or MatchTargetLateral.");
        }
        return warnings.ToArray();
    }

    private static bool IsNonNegative(float value) => float.IsFinite(value) && value >= 0f;

    /// <summary>True when every numeric export is within the range the orbit arithmetic expects.</summary>
    private bool HasUsableConfiguration
        => float.IsFinite(Radius) && Radius > 0f
           && float.IsFinite(RadiusSpread) && RadiusSpread >= 0f && RadiusSpread < Radius
           && float.IsFinite(RadialSoftness) && RadialSoftness > 0f
           && float.IsFinite(FlipIntervalSeconds) && FlipIntervalSeconds >= 0f
           && IsNonNegative(MirrorDeadzone) && IsNonNegative(HoldStandSpeed) && IsNonNegative(HoldBand);

    private static TargetMember NearestMember(TargetSample sample)
    {
        var nearest = sample.Members[0];
        foreach (var member in sample.Members)
        {
            if (member.FlatDistance < nearest.FlatDistance) { nearest = member; }
        }
        return nearest;
    }

    #region Test Helpers
#if TOOLS
    internal void SetRadiusForTest(float value) => Radius = value;
    internal void SetRadiusSpreadForTest(float value) => RadiusSpread = value;
    internal void SetFlipIntervalForTest(float value) => FlipIntervalSeconds = value;
    internal void SetRadialSoftnessForTest(float value) => RadialSoftness = value;
    internal void SetSignSourceForTest(OrbitSignSource value) => SignSource = value;
    internal void SetHoldBandForTest(float value) => HoldBand = value;
    internal void SetMirrorDeadzoneForTest(float value) => MirrorDeadzone = value;
#endif
    #endregion
}
