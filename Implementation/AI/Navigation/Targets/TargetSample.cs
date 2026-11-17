namespace Jmodot.Implementation.AI.Navigation.Targets;

using System.Collections.Generic;
using Core.AI.Navigation.Considerations;
using Core.AI.Perception;
using Jmodot.Implementation.AI.Navigation.Considerations;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>Whether a resolve produced members, found none, or could not run at all.</summary>
public enum TargetSampleState
{
    /// <summary>At least one eligible member was resolved.</summary>
    Resolved,

    /// <summary>The target is configured and queryable, but holds no eligible member right now.</summary>
    Empty,

    /// <summary>The target cannot run: its authored configuration or the agent prerequisite is unusable.</summary>
    Unavailable,
}

/// <summary>The shape of a resolved sample: one selected member, or the whole eligible set.</summary>
public enum TargetSampleGeometry
{
    Member,
    Set,
}

/// <summary>
/// One resolved target: the point a goal steers at or ends on. Built once per resolve, so a
/// scoring pass never rereads a perception memory that may change between members.
/// <see cref="Position"/> and <see cref="ProjectedPosition"/> keep their Y; every distance is XZ.
/// </summary>
public readonly struct TargetMember
{
    /// <summary>The perception memory this member came from; null for a hand-built math sample.</summary>
    public readonly Perception3DInfo? Memory;

    public readonly ulong InstanceId;

    /// <summary>Last-known raw position, vertical component retained.</summary>
    public readonly Vector3 Position;

    public readonly Vector3 Velocity;

    /// <summary>Confidence captured at resolve time.</summary>
    public readonly float Confidence;

    /// <summary>Fully velocity-projected position, vertical component retained.</summary>
    public readonly Vector3 ProjectedPosition;

    /// <summary>XZ distance from the agent to <see cref="Position"/>.</summary>
    public readonly float FlatDistance;

    public TargetMember(Perception3DInfo? memory, ulong instanceId, Vector3 position, Vector3 velocity,
        float confidence, Vector3 projectedPosition, float flatDistance)
    {
        Memory = memory;
        InstanceId = instanceId;
        Position = position;
        Velocity = velocity;
        Confidence = confidence;
        ProjectedPosition = projectedPosition;
        FlatDistance = flatDistance;
    }

    /// <summary>Captures one live perception memory relative to <paramref name="agentPosition"/>.</summary>
    public static TargetMember FromMemory(Perception3DInfo memory, Vector3 agentPosition)
        => new(
            memory,
            memory.Target.GetInstanceId(),
            memory.LastKnownPosition,
            memory.LastKnownVelocity,
            memory.CurrentConfidence,
            memory.GetProjectedPosition(),
            JmoMath.HorizontalDistance(agentPosition, memory.LastKnownPosition));
}

/// <summary>
/// Per-agent resolve output. A goal or consideration fills one sample per tick and scores only
/// that sample, so membership, point and distance agree within the tick.
/// </summary>
public sealed class TargetSample
{
    private readonly List<TargetMember> _members = new();

    /// <summary>True when this sample belongs to a per-agent runtime that owns its diagnostic latches.</summary>
    internal TargetRuntime? Owner { get; }

    internal TargetSample(TargetRuntime owner) => Owner = owner;

    /// <summary>A standalone sample with no runtime-owned latch; callers outside a processor use this.</summary>
    public TargetSample() { }

    public IReadOnlyList<TargetMember> Members => _members;

    public TargetSampleState State { get; internal set; }

    public TargetSampleGeometry Geometry { get; internal set; }

    /// <summary>Flat (Y = 0) position of the nearest member; zero when the sample holds none.</summary>
    public Vector3 Point { get; internal set; }

    /// <summary>Flat distance to the nearest member; meaningful only while <see cref="State"/> is Resolved.</summary>
    public float NearestDistance { get; internal set; }

    /// <summary>Resolution captured from the policy on the last resolve; 0 for a tracked target.</summary>
    public float Resolution { get; internal set; }

    /// <summary>Focus captured from the policy on the last resolve; 0 for a tracked target.</summary>
    public float Focus { get; internal set; }

    public bool HasMembers => _members.Count > 0;

    /// <summary>Empties the sample before a resolve, so a failed resolve leaves no stale member or point.</summary>
    public void Clear()
    {
        _members.Clear();
        State = TargetSampleState.Empty;
        Geometry = TargetSampleGeometry.Member;
        Point = Vector3.Zero;
        NearestDistance = 0f;
        Resolution = 0f;
        Focus = 0f;
    }

    internal void SetSingle(TargetMember member)
    {
        _members.Clear();
        _members.Add(member);
        Geometry = TargetSampleGeometry.Member;
        State = TargetSampleState.Resolved;
        RecomputePoint();
    }

    internal void Adopt(List<TargetMember> members, TargetSampleGeometry geometry,
        float resolution = 0f, float focus = 0f)
    {
        _members.Clear();
        _members.AddRange(members);
        Geometry = geometry;
        Resolution = resolution;
        Focus = focus;
        State = members.Count > 0 ? TargetSampleState.Resolved : TargetSampleState.Empty;
        RecomputePoint();
    }

    /// <summary>Appends one member while a resolve is filling the sample; <see cref="Seal"/> finishes it.</summary>
    internal void AddMember(TargetMember member) => _members.Add(member);

    /// <summary>Finishes a fill: records the shape and policy numbers and derives state and point.</summary>
    internal void Seal(TargetSampleGeometry geometry, float resolution = 0f, float focus = 0f)
    {
        Geometry = geometry;
        Resolution = resolution;
        Focus = focus;
        State = _members.Count > 0 ? TargetSampleState.Resolved : TargetSampleState.Empty;
        RecomputePoint();
    }

    /// <summary>Drops every member except <paramref name="member"/>, in place.</summary>
    internal void KeepOnly(TargetMember member)
    {
        _members.Clear();
        _members.Add(member);
    }

    internal void RecomputePoint()
    {
        var nearest = float.MaxValue;
        var point = Vector3.Zero;
        foreach (var member in _members)
        {
            if (member.FlatDistance >= nearest) { continue; }
            nearest = member.FlatDistance;
            point = new Vector3(member.Position.X, 0f, member.Position.Z);
        }

        Point = point;
        NearestDistance = _members.Count == 0 ? 0f : nearest;
    }

    /// <summary>True when this resolve may emit its diagnostic; false once its owner logged it.</summary>
    internal bool TryBeginLog(TargetLogKind kind) => Owner?.MarkLogged(kind) ?? true;
}

/// <summary>
/// Per-agent state for a targeted goal or consideration: its sample, a reusable response buffer
/// and the diagnostic latches that keep a runtime fault to one log per agent.
/// </summary>
public class TargetRuntime : AIConsiderationRuntime
{
    public TargetRuntime() => Sample = new TargetSample(this);

    public TargetSample Sample { get; }

    /// <summary>Reusable per-member response scratch for the shared response engine.</summary>
    public List<TargetResponse> ResponseBuffer { get; } = new();

    internal bool EmptyKeyLogged;
    internal bool MissingKeyLogged;
    internal bool ConfigErrorLogged;
    /// <summary>The wrong-typed zone key latch <see cref="Considerations.ZoneSuppression"/> reads by reference.</summary>
    internal bool ZoneKeyWrongTypeLogged;

    /// <summary>True on the first call for a latch, false afterwards.</summary>
    internal bool MarkLogged(TargetLogKind kind)
    {
        bool logged;
        switch (kind)
        {
            case TargetLogKind.EmptyKey: logged = EmptyKeyLogged; EmptyKeyLogged = true; break;
            case TargetLogKind.MissingKey: logged = MissingKeyLogged; MissingKeyLogged = true; break;
            default: logged = ConfigErrorLogged; ConfigErrorLogged = true; break;
        }
        return !logged;
    }
}

internal enum TargetLogKind
{
    EmptyKey,
    MissingKey,
    ConfigError,
}
