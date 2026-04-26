namespace Jmodot.Implementation.Actors;

using System;
using System.Collections.Generic;
using Core.Movement;
using Core.Pooling;

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

    public event Action<ImpactInfo>? Impacted;

    private ICharacterController3D? _controller;
    private CharacterBody3D? _body;

    private HashSet<ulong> _inContactLastFrame = new();
    private HashSet<ulong> _newContactsThisFrame = new();

    public void Initialize(ICharacterController3D controller, CharacterBody3D body)
    {
        _controller = controller;
        _body = body;
        _inContactLastFrame.Clear();
        _newContactsThisFrame.Clear();
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
        // can suspend detection — do NOT couple to ForceControlLossDetector or any other specific
        // consumer (defeats the modular impact contract). MinImpactSpeed already filters most
        // trivial contacts pre-allocation.
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
            var info = new ImpactInfo(preMoveSpeed, col.GetNormal(), collider);
            Impacted?.Invoke(info);
        }

        (_inContactLastFrame, _newContactsThisFrame) = (_newContactsThisFrame, _inContactLastFrame);
    }
}

/// <summary>
/// Raw collision facts emitted by <see cref="ImpactDetector"/>. No classification —
/// geometry queried via dot-product helpers; identity queried via the project's
/// Category system on <see cref="Collider"/>.
/// </summary>
public readonly record struct ImpactInfo(float Speed, Godot.Vector3 Normal, Node3D Collider)
{
    /// <summary>True if the contact normal is closer to up than to horizontal (floor-like).</summary>
    public bool IsHorizontalSurface(float threshold = 0.7f) => Normal.Dot(Godot.Vector3.Up) > threshold;

    /// <summary>True if the contact normal is roughly perpendicular to up (wall-like).</summary>
    public bool IsWall(float threshold = 0.3f) => Math.Abs(Normal.Dot(Godot.Vector3.Up)) < threshold;

    /// <summary>True if the contact normal points down (ceiling-like).</summary>
    public bool IsCeiling(float threshold = 0.7f) => Normal.Dot(Godot.Vector3.Up) < -threshold;
}
