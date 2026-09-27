namespace Jmodot.Implementation.Physics;

using System;
using System.Collections.Generic;
using Godot;
using Shared;

/// <summary>Result of one queued ground probe.</summary>
public enum GroundingOutcome
{
    /// <summary>The body now rests on its support.</summary>
    Grounded,
    /// <summary>Nothing under the probe (or no collider extent); the body kept its placement.</summary>
    Missed,
    /// <summary>The body was freed or left the tree before the tick; nothing was probed.</summary>
    Unprobeable,
}

/// <summary>
/// The one owner of "ground this body on the next physics tick". Any spawn path (player entry, encounter
/// spawns, summons) places a body and calls <see cref="Request"/>; the queue probes on its own
/// <see cref="_PhysicsProcess"/>, when bodies added during the placing frame are queryable, and applies
/// <see cref="BodyGroundSnapper.TryGround"/>. Consumers never schedule frames themselves.
/// </summary>
/// <remarks>
/// The consuming project hosts exactly one instance in its always-present tree (an autoload child) and
/// reads it through <see cref="Current"/>; the instance publishes itself on enter and clears on exit.
/// A request with no queue in the tree is a configuration error and throws, never a silent no-op.
/// A miss is reported once, on the tick that probed, unless the caller supplied <c>onResolved</c>: the
/// callback then owns every outcome, including <see cref="GroundingOutcome.Unprobeable"/> for a body
/// freed before the tick (a batch that counts outcomes must still hear about it).
/// </remarks>
public partial class GroundingQueue : Node
{
    private readonly struct Pending
    {
        public Pending(PhysicsBody3D body, GodotObject logContext, Action<GroundingOutcome>? onResolved)
        {
            Body = body;
            LogContext = logContext;
            OnResolved = onResolved;
        }

        public PhysicsBody3D Body { get; }
        public GodotObject LogContext { get; }
        public Action<GroundingOutcome>? OnResolved { get; }
    }

    private static GroundingQueue? _current;
    private readonly List<Pending> _pending = new();

    /// <summary>The live queue, or null before the hosting tree has entered.</summary>
    public static GroundingQueue? Current => _current;

    /// <summary>Requires the live queue; throws when the consuming project forgot to host one.</summary>
    public static GroundingQueue Required(GodotObject requester)
        => _current ?? throw new InvalidOperationException(
            $"{requester} requested grounding but no GroundingQueue is in the tree. Host one under an always-present node.");

    /// <summary>Test seam: forgets the published instance without touching the tree.</summary>
    internal static void Reset() => _current = null;

    public int PendingCount => _pending.Count;

    public override void _EnterTree()
    {
        if (_current != null && _current != this && GodotObject.IsInstanceValid(_current))
        {
            JmoLogger.Warning(this, $"[GroundSnap] A second GroundingQueue entered the tree ('{Name}'); the newer one is now Current.");
        }
        _current = this;
    }

    public override void _ExitTree()
    {
        if (_current == this) { _current = null; }
        _pending.Clear();
    }

    /// <summary>
    /// Grounds <paramref name="body"/> on the next physics tick. Re-requesting a body already pending
    /// replaces the earlier request, so the newest placement wins.
    /// </summary>
    public void Request(PhysicsBody3D body, GodotObject logContext, Action<GroundingOutcome>? onResolved = null)
    {
        _pending.RemoveAll(p => p.Body == body);
        _pending.Add(new Pending(body, logContext, onResolved));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pending.Count == 0) { return; }
        Pending[] batch = _pending.ToArray();
        _pending.Clear();
        foreach (Pending p in batch)
        {
            Resolve(p);
        }
    }

    private static void Resolve(Pending p)
    {
        PhysicsBody3D body = p.Body;
        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree() || body.IsQueuedForDeletion())
        {
            p.OnResolved?.Invoke(GroundingOutcome.Unprobeable);
            return;
        }

        bool grounded = BodyGroundSnapper.TryGround(body, body.GlobalTransform, out Transform3D result);
        if (grounded) { body.GlobalTransform = result; }

        if (p.OnResolved != null)
        {
            p.OnResolved(grounded ? GroundingOutcome.Grounded : GroundingOutcome.Missed);
            return;
        }
        if (grounded) { return; }

        GodotObject context = GodotObject.IsInstanceValid(p.LogContext) ? p.LogContext : body;
        JmoLogger.Warning(context,
            $"[GroundSnap] '{body.Name}' at {body.GlobalPosition} could not be grounded (nothing under the probe, "
            + "or no collider extent to measure); leaving it at its placement height.");
    }
}
