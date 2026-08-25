namespace Jmodot.Implementation.Actors;

using System.Collections.Generic;
using Godot;
using Core.Actors;
using Core.Movement;
using Core.Movement.Strategies;
using Core.Stats;
using Movement.Strategies;
using Shared;

/// <summary>
///     The definitive high-level orchestrator for character movement. Its sole responsibility
///     is to be a pure calculation engine. It takes the active strategy, the character's final
///     modified stats, and a pre-calculated desired direction, and uses them to calculate the
///     final velocity command for the IMovementController. It is a reusable, stateless service
///     called by the character's State Machine.
/// </summary>
public class MovementProcessor3D : IMovementProcessor3D
{
    private readonly ICharacterController3D _controller;
    private readonly ExternalForceReceiver3D _forceReceiver3D;
    private readonly Node3D _owner;
    private readonly IStatProvider _stats;
    private readonly Attribute? _stabilityAttr;

    private Vector3 _frameImpulses = Vector3.Zero;
    private bool _frameImpulsesReplace;
    private Vector3 _previousDirection;
    private readonly HashSet<int> _warnedTurnLogicConflicts = new();

    private readonly IMovementStrategy3D? _default;
    private IMovementStrategy3D? _override;

    private readonly OwnedSlot<bool> _suspensionSlot = new("Movement");

    public MovementProcessor3D(
        ICharacterController3D controller,
        IStatProvider statsProvider,
        ExternalForceReceiver3D forceReceiver3D,
        Node3D owner,
        Attribute? stabilityAttr = null,
        IMovementStrategy3D? defaultStrategy = null)
    {
        this._controller = controller;
        this._stats = statsProvider;
        this._forceReceiver3D = forceReceiver3D;
        this._owner = owner;
        this._stabilityAttr = stabilityAttr;
        this._default = defaultStrategy;
    }

    public IMovementStrategy3D? Default => _default;

    public IMovementStrategy3D? ActiveStrategy => _override ?? _default;

    public void SetStrategyOverride(IMovementStrategy3D strategy)
    {
        if (_override != null && !ReferenceEquals(_override, strategy))
        {
            JmoLogger.Warning(this,
                $"SetStrategyOverride conflict: replacing {_override.GetType().Name} with {strategy.GetType().Name}. " +
                "Slot is single-writer-at-a-time by convention; concurrent writers indicate a design smell.");
        }
        _override = strategy;
    }

    public void ClearStrategyOverride()
    {
        if (_override == null)
        {
            JmoLogger.Warning(this, "ClearStrategyOverride called when no override was active.");
            return;
        }
        _override = null;
    }

    public bool IsSuspended => _suspensionSlot.IsClaimed;

    public bool TryClaimSuspension(StringName owner, SuspensionVelocityPolicy velocityPolicy = SuspensionVelocityPolicy.Preserve)
    {
        if (!_suspensionSlot.TryClaim(owner, true, _owner, "Movement suspension"))
        {
            return false;
        }

        // Impulses are discarded, not queued: without this drain, every knockback landed while
        // suspended would sum in _frameImpulses and discharge as one launch on release.
        ClearImpulses();
        if (velocityPolicy == SuspensionVelocityPolicy.Zero) { _controller.SetVelocity(Vector3.Zero); }

        return true;
    }

    public void ReleaseSuspension(StringName owner)
    {
        _suspensionSlot.TryRelease(owner, _owner, "Movement suspension");
    }

    public void ProcessMovement(Vector3 desiredDirection, float delta)
    {
        if (IsSuspended)
        {
            ClearImpulses();
            return;
        }

        var active = ActiveStrategy;
        if (active == null)
        {
            throw new System.InvalidOperationException(
                "MovementProcessor3D.ProcessMovement(direction, delta) called with neither override nor Default set. " +
                "Pass a defaultStrategy at construction or call SetStrategyOverride first.");
        }
        ProcessMovement(active, desiredDirection, delta);
    }

    /// <summary>
    ///     The main update loop for continuous movement. It is called by the active State,
    ///     which provides all necessary contextual information.
    /// </summary>
    public void ProcessMovement(IMovementStrategy3D strategy3D, Vector3 desiredDirection, float delta)
    {
        if (IsSuspended)
        {
            ClearImpulses();
            return;
        }

        // --- 0. Pre-process Turn Rate (if strategy has a composable TurnProfile) ---
        var inputVelocity = this._controller.Velocity;

        if (strategy3D is BaseMovementStrategy3D { TurnProfile: { } profile } baseStrategy)
        {
            if (baseStrategy.HasInternalTurnLogic && _warnedTurnLogicConflicts.Add(baseStrategy.GetHashCode()))
            {
                JmoLogger.Warning(baseStrategy,
                    "TurnProfile is set on a strategy with internal turn logic. Both will apply.");
            }

            desiredDirection = profile.Apply(_previousDirection, desiredDirection, inputVelocity, _stats, delta);
        }

        // --- 1. Calculate Character-Driven Velocity via the Strategy ---
        var characterVelocity =
            strategy3D.CalculateVelocity(inputVelocity, desiredDirection, _previousDirection, this._stats, delta);

        // Update previous direction from strategy output (reflects any turn rate clamping)
        var flatVel = new Vector3(characterVelocity.X, 0, characterVelocity.Z);
        if (!flatVel.IsZeroApprox()) { _previousDirection = flatVel.Normalized(); }

        _controller.SetVelocity(characterVelocity);

        // --- 2. Apply Impulses (stored in velocity) ---
        DrainImpulses();

        // --- 3. Apply External Forces (stored - will be affected by friction next frame) ---
        ApplyExternalForces(delta);

        // --- 4. Get Velocity Offset (NOT stored - fresh each frame, friction-independent) ---
        // Apply stability scaling BEFORE the collision-delta isolation logic below,
        // so the already-scaled offset participates in both the Move() and the delta extraction.
        var velocityOffset = ScaleByStability(_forceReceiver3D.GetTotalVelocityOffset(_owner));

        // --- 5. Execute the Final Move with offset ---
        // Store base velocity before adding offset, so we can isolate collision effects
        var baseVelocity = _controller.Velocity;
        var combined = baseVelocity + velocityOffset;
        _controller.SetVelocity(combined);
        _controller.Move();

        // --- 6. Isolate collision delta and apply to base velocity only ---
        // After MoveAndSlide, velocity may differ from combined due to collisions.
        // We extract what collision changed and apply that to the base velocity,
        // discarding the offset cleanly without corrupting post-collision velocity.
        var postCollision = _controller.Velocity;
        this.WarnOnLaunch(characterVelocity, combined, postCollision);
        var collisionDelta = postCollision - combined;
        _controller.SetVelocity(baseVelocity + collisionDelta);
    }

    /// <summary>
    /// Warns when post-move speed exceeds twice the resolved speed, with a 30 m/s floor.
    /// </summary>
    private void WarnOnLaunch(Vector3 resolvedVelocity, Vector3 preMoveVelocity, Vector3 postMoveVelocity)
    {
        var resolvedMaxSpeed = new Vector3(resolvedVelocity.X, 0f, resolvedVelocity.Z).Length();
        var threshold = Mathf.Max(30f, 2f * resolvedMaxSpeed);
        if (!(postMoveVelocity.Length() > threshold)) { return; }

        var collisions = new List<string>();
        if (this._owner is CharacterBody3D body)
        {
            for (var i = 0; i < body.GetSlideCollisionCount(); i++)
            {
                var collider = body.GetSlideCollision(i).GetCollider();
                collisions.Add(collider is Node node ? node.Name : collider?.GetType().Name ?? "<null>");
            }
        }

        JmoLogger.Warning(this,
            $"[Movement] launch speed={postMoveVelocity.Length():F2} threshold={threshold:F2} "
            + $"pre={preMoveVelocity} post={postMoveVelocity} floor={this._controller.IsOnFloor} "
            + $"slides={collisions.Count} colliders=[{string.Join(", ", collisions)}]");
    }

    /// <summary>
    ///     An update loop for states where the character is passive (e.g., stunned, interacting).
    ///     It does not run a movement strategy but still applies gravity and other external forces.
    /// </summary>
    public void ProcessExternalForcesOnly(float delta)
    {
        if (IsSuspended)
        {
            ClearImpulses();
            return;
        }

        // No strategy is run. We respect the velocity set by other systems (e.g., knockback impulse).
        // 1. Still apply any impulses that might occur
        DrainImpulses();

        // 2. Apply external forces
        this.ApplyExternalForces(delta);

        // 3. Get velocity offset (friction-independent)
        // Apply stability scaling BEFORE collision-delta isolation (see ProcessMovement for rationale).
        var velocityOffset = ScaleByStability(_forceReceiver3D.GetTotalVelocityOffset(_owner));

        // 4. Execute the move with offset, isolating collision effects
        var baseVelocity = _controller.Velocity;
        var combined = baseVelocity + velocityOffset;
        _controller.SetVelocity(combined);
        _controller.Move();

        // 5. Apply collision delta to base velocity only
        var postCollision = _controller.Velocity;
        var collisionDelta = postCollision - combined;
        _controller.SetVelocity(baseVelocity + collisionDelta);
    }

    /// <summary>
    /// Settle-only tick for recovery states (WallHit / GroundFall): applies pending
    /// impulses + Move(), but skips ExternalForceReceiver aggregate. Prevents sustained
    /// environmental forces (e.g. wave drag) from re-launching the entity mid-recovery.
    /// </summary>
    public void ProcessImpulsesOnly(float delta)
    {
        if (IsSuspended)
        {
            ClearImpulses();
            return;
        }

        DrainImpulses();
        _controller.Move();
    }

    /// <summary>
    ///     Applies an instantaneous change in velocity to the character controller.
    ///     This is the primary method for all impulse-based mechanics.
    /// </summary>
    /// <param name="impulse">The velocity vector to add to the character's current velocity.</param>
    /// <param name="mode">
    /// <see cref="ImpulseMode.Replace"/> discards any impulse already queued this frame and latches
    /// the frame to SET rather than add, so the strategy's velocity is overridden too. A later Add
    /// in the same frame composes on top of the replaced value; a later Replace wins outright.
    /// </param>
    public void ApplyImpulse(Vector3 impulse, ImpulseMode mode = ImpulseMode.Add)
    {
        if (mode == ImpulseMode.Replace)
        {
            _frameImpulses = impulse;
            _frameImpulsesReplace = true;
            return;
        }

        _frameImpulses += impulse;
    }

    public void ClearImpulses()
    {
        _frameImpulses = Vector3.Zero;
        _frameImpulsesReplace = false;
    }

    /// <summary>
    /// Hands the frame's accumulated impulse to the controller and clears it. Replace overrides
    /// whatever the strategy just wrote; Add composes on top of it.
    /// </summary>
    private void DrainImpulses()
    {
        if (_frameImpulsesReplace)
        {
            _controller.SetVelocity(_frameImpulses);
        }
        else
        {
            _controller.AddVelocity(_frameImpulses);
        }

        ClearImpulses();
    }

    private void ApplyExternalForces(float delta)
    {
        var externalForce = this._forceReceiver3D.GetTotalForce(this._owner);
        this._controller.AddVelocity(ScaleByStability(externalForce) * delta);
    }

    /// <summary>
    /// Scales an incoming force/offset vector by the actor's stability resistance factor.
    /// Returns the input unchanged when no stability attribute is wired. This is the single
    /// consumer site; route all external force/offset reads through here to avoid double-dipping.
    /// </summary>
    private Vector3 ScaleByStability(Vector3 force)
    {
        if (_stabilityAttr == null)
        {
            return force;
        }

        var stability = _stats.GetStatValue<float>(_stabilityAttr, 0f);
        return StabilityScaling.ScaleForce(force, stability);
    }
}
