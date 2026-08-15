namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System.Collections.Generic;
using AI.Animation;
using Core.Movement;
using Core.Visual.Animation.Sprite;
using Godot;

/// <summary>
/// The locomotion rung of the entity fallback tier: an unclaimed body plays its move clip while it
/// is travelling and its idle clip while it is at rest, with a hysteresis band between the two
/// thresholds so a body hovering at the boundary does not restart a clip every frame.
/// </summary>
[GlobalClass, Tool]
public partial class LocomotionFallbackProfile3D : AnimationFallbackProfile3D
{
    /// <summary>Base clip played while the body is at rest and unclaimed.</summary>
    [Export] public StringName IdleAnim { get; private set; } = new("idle");

    /// <summary>Base clip played while the body is moving and unclaimed.</summary>
    [Export] public StringName MoveAnim { get; private set; } = new("run");

    /// <summary>Flat speed (m/s) at or above which a still body starts moving.</summary>
    [Export] public float MoveEnterSpeed { get; private set; } = 0.35f;

    /// <summary>
    /// Flat speed (m/s) at or below which a moving body settles. Keep below MoveEnterSpeed — the gap
    /// is the anti-chatter band.
    /// </summary>
    [Export] public float MoveExitSpeed { get; private set; } = 0.15f;

    public override IEnumerable<StringName> AuthoredClips
    {
        get
        {
            yield return this.IdleAnim;
            yield return this.MoveAnim;
        }
    }

    public override StringName? ResolveClip(ICharacterController3D controller, float delta, StringName? previousResolvedClip)
    {
        var flat = controller.Velocity;
        flat.Y = 0f;

        var wasMoving = previousResolvedClip != null && previousResolvedClip == this.MoveAnim;
        var isMoving = LocomotionAnimResolver.ResolveIsMoving(
            flat.LengthSquared(),
            wasMoving,
            this.MoveEnterSpeed * this.MoveEnterSpeed,
            this.MoveExitSpeed * this.MoveExitSpeed);

        return isMoving ? this.MoveAnim : this.IdleAnim;
    }

    public override string? ValidateConfiguration()
    {
        if (this.MoveEnterSpeed <= 0f)
        {
            return $"MoveEnterSpeed ({this.MoveEnterSpeed}) must be positive; a non-positive enter threshold "
                   + "degenerates the hysteresis band and the body never enters the move state.";
        }
        if (this.MoveExitSpeed * this.MoveExitSpeed > this.MoveEnterSpeed * this.MoveEnterSpeed)
        {
            return $"MoveExitSpeed ({this.MoveExitSpeed}) exceeds MoveEnterSpeed ({this.MoveEnterSpeed}); "
                   + "the hysteresis band is inverted and the body will read as permanently moving.";
        }
        return null;
    }

    #region Test Helpers
#if TOOLS
    internal void SetForTest(StringName idleAnim, StringName moveAnim, float moveEnterSpeed, float moveExitSpeed)
    {
        this.IdleAnim = idleAnim;
        this.MoveAnim = moveAnim;
        this.MoveEnterSpeed = moveEnterSpeed;
        this.MoveExitSpeed = moveExitSpeed;
    }
#endif
    #endregion
}
