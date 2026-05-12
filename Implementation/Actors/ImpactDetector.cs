namespace Jmodot.Implementation.Actors;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.Combat;
using Core.Combat.Reactions;
using Core.Movement;
using Core.Pooling;
using Implementation.AI.BB;

/// <summary>
/// Raw collision-event detector for character-body actors. Emits one
/// <see cref="Impacted"/> event per rising-edge collider contact whose pre-move
/// velocity exceeded <see cref="MinImpactSpeed"/>. Does not classify hits — consumers
/// query <see cref="ImpactInfo"/>'s normal-math helpers (geometry) or the project
/// Category system on <see cref="ImpactInfo.Collider"/> (identity).
/// </summary>
[GlobalClass]
public partial class ImpactDetector : Node, IPoolResetable
{
    /// <summary>Pre-move velocity magnitude required for a contact to count as an impact.</summary>
    [Export(PropertyHint.Range, "0.1,100,0.1")]
    public float MinImpactSpeed { get; set; } = 6f;

    public event Action<ImpactInfo> Impacted = delegate { };

    private ICharacterController3D? _controller;
    private CharacterBody3D? _body;
    private CombatLog? _combatLog;

    private HashSet<ulong> _inContactLastFrame = new();
    private HashSet<ulong> _newContactsThisFrame = new();

    /// <summary>
    /// Wires the detector's runtime dependencies. <paramref name="bb"/> is optional — when
    /// provided, ImpactDetector logs each rising-edge contact to <c>CombatLog</c> as an
    /// <see cref="ImpactResult"/>, enabling HSM-side queryable lookups
    /// (e.g., WallImpactCondition reading <c>GetAllCombatResultsWithinCombatTime&lt;ImpactResult&gt;</c>).
    /// When null, the event-only path remains active for direct subscribers.
    /// </summary>
    public void Initialize(ICharacterController3D controller, CharacterBody3D body, IBlackboard? bb = null)
    {
        _controller = controller;
        _body = body;
        _inContactLastFrame.Clear();
        _newContactsThisFrame.Clear();

        // Soft dep — entities without combat (props, destructibles) won't have CombatLog.
        // Mirrors KnockbackComponent3D.Initialize CombatLog resolution.
        if (bb != null)
        {
            bb.TryGet(BBDataSig.CombatLog, out _combatLog);
        }
    }

    public void OnPoolReset()
    {
        _inContactLastFrame.Clear();
        _newContactsThisFrame.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        // TODO(perf): swarm scenarios with N>30 actors will multiply slide-collision iteration cost.
        // If profiling shows this hot, add a generic [Export] bool Enabled toggle so consumers
        // can suspend detection — do NOT couple to any specific consumer (defeats the modular
        // impact contract). MinImpactSpeed already filters most trivial contacts pre-allocation.
        if (_controller == null || _body == null)
        {
            return;
        }

        _newContactsThisFrame.Clear();
        var preMoveSpeed = _controller.PreMoveVelocity.Length();

        if (preMoveSpeed < MinImpactSpeed)
        {
            // Below threshold — no impacts qualify. Reset BOTH buffers so a future high-speed
            // re-contact with the same collider fires as a rising edge.
            _inContactLastFrame.Clear();
            _newContactsThisFrame.Clear();
            return;
        }

        var slideCount = _body.GetSlideCollisionCount();
        for (var i = 0; i < slideCount; i++)
        {
            var col = _body.GetSlideCollision(i);
            if (col?.GetCollider() is not Node3D collider)
            {
                continue;
            }

            var id = collider.GetInstanceId();
            if (_inContactLastFrame.Contains(id))
            {
                // Sustained contact — already-emitted this collider this contact-window.
                _newContactsThisFrame.Add(id);
                continue;
            }

            _newContactsThisFrame.Add(id);
            var normal = col.GetNormal();
            var speedAlongNormal = ImpactInfo.ComputeSpeedAlongNormal(_controller.PreMoveVelocity, normal);
            var info = new ImpactInfo(speedAlongNormal, normal, collider);
            Impacted.Invoke(info);

            // Dual-channel publish: event for sibling damage systems (per-frame subscribe);
            // CombatLog for HSM-side queryable lookback (TransitionCondition.CheckEvent reads).
            // Mirrors KnockbackComponent3D pattern: emit signal AND log CombatResult.
            _combatLog?.Log(new ImpactResult(collider, normal, speedAlongNormal));
        }

        (_inContactLastFrame, _newContactsThisFrame) = (_newContactsThisFrame, _inContactLastFrame);
    }

    #region Test Helpers
#if TOOLS
    /// <summary>
    /// Test-only emit of <see cref="Impacted"/> with a synthetic <see cref="ImpactInfo"/>.
    /// Bypasses the velocity/slide-collision machinery so consumer-side tests can drive
    /// CapturedState's wall-impact transition (and similar) without setting up a full
    /// physics scene.
    /// </summary>
    /// <remarks>
    /// Wrapped in <c>#if TOOLS</c> per the project's test-helper convention so it does
    /// NOT ship in release builds. C# events can only be invoked from within their
    /// declaring class — this is the formal escape hatch.
    /// </remarks>
    internal void EmitImpactedForTesting(ImpactInfo info)
    {
        Impacted.Invoke(info);
    }
#endif
    #endregion
}

/// <summary>
/// Raw collision facts emitted by <see cref="ImpactDetector"/>. No classification —
/// geometry queried via dot-product helpers; identity queried via the project's
/// Category system on <see cref="Collider"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>Speed</c> semantics: the magnitude of the pre-move velocity component
/// along the contact normal (perpendicular impact severity), clamped to ≥0.
/// A grunt sliding horizontally on the floor reports near-zero Speed because
/// the velocity is perpendicular to the floor normal. Use
/// <see cref="ComputeSpeedAlongNormal"/> at construction time.
/// </para>
/// </remarks>
public readonly record struct ImpactInfo(float Speed, Godot.Vector3 Normal, Node3D Collider)
{
    /// <summary>
    /// Projects <paramref name="preMoveVelocity"/> onto the inverted contact normal
    /// (the direction the body was moving into the surface) and clamps to ≥0.
    /// </summary>
    /// <remarks>
    /// Contact normals in Godot point AWAY from the surface (out toward the colliding body).
    /// A body moving INTO the surface has velocity roughly OPPOSITE to the normal, so
    /// <c>velocity.Dot(normal)</c> is negative; negating gives a positive impact speed.
    /// The <see cref="MathF.Max"/> guard handles kinematic edge cases where the body is
    /// already exiting contact (positive dot → would otherwise produce a negative "impact").
    /// </remarks>
    public static float ComputeSpeedAlongNormal(Godot.Vector3 preMoveVelocity, Godot.Vector3 contactNormal)
        => MathF.Max(0f, -preMoveVelocity.Dot(contactNormal));

    /// <summary>True if the contact normal is closer to up than to horizontal (floor-like).</summary>
    public bool IsHorizontalSurface(float threshold = 0.7f) => Normal.Dot(Godot.Vector3.Up) > threshold;

    /// <summary>True if the contact normal is roughly perpendicular to up (wall-like).</summary>
    public bool IsWall(float threshold = 0.3f) => Math.Abs(Normal.Dot(Godot.Vector3.Up)) < threshold;

    /// <summary>True if the contact normal points down (ceiling-like).</summary>
    public bool IsCeiling(float threshold = 0.7f) => Normal.Dot(Godot.Vector3.Up) < -threshold;
}
