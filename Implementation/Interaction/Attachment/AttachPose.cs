namespace Jmodot.Implementation.Interaction.Attachment;

using Godot;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// One attach VISUAL a rider can occupy on a host: an identity plus the two clips its art plays
/// while it is held, and a host-local offset for where the rider sits. Pose art is drawn on a
/// host-sized canvas, so by default the rider rides at the host's origin; a non-zero
/// <see cref="PoseOffset"/> lets one pose shift within that silhouette.
///
/// <para>
/// <b>Rider-owned, not host-owned.</b> The art is the RIDER's, so the same set travels to any host
/// with zero host authoring; a host only tracks which <see cref="Id"/>s its current riders hold.
/// Where riders sit on a silhouette remains the host's decision
/// (<see cref="AttachmentAnchorProfile3D"/>) and stays independent of pose identity.
/// </para>
///
/// <para>
/// <b>Ids are single-token camelCase compounds</b> (<c>front</c>, <c>frontSide</c>): the clip name is
/// composed as <c>attach_&lt;id&gt;</c>, and the animation-visibility coordinator keys a managed node
/// off the clip name truncated at its LAST separator — a second underscore would key to the wrong
/// node and silently show nothing.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class AttachPose : Resource
{
    /// <summary>Identity a host's ledger books this pose under. Unique within its set, non-empty, single-token camelCase.</summary>
    [Export] public StringName Id { get; private set; } = "";

    /// <summary>Clip played while the rider is riding in this pose.</summary>
    [Export] public StringName RideAnimationName { get; private set; } = "";

    /// <summary>Clip played while the rider attacks from this pose.</summary>
    [Export] public StringName AttackAnimationName { get; private set; } = "";

    /// <summary>How long one attack tick's animation claim survives. Clip-bound art data, not a combat
    /// tuning number: it is pinned by the attack clip's length, so per-pose because a multi-pose rider
    /// has attack clips of differing length. Zero falls back to the scheduler's tick interval.</summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.05,suffix:s")]
    public float AttackAnimationHoldSeconds { get; private set; }

    /// <summary>Host-local offset from the host's origin this pose rides at. Zero (the default) rides at
    /// the origin; the offset survives host rotation because the anchor system operates in host-local
    /// space.</summary>
    [Export] public Vector3 PoseOffset { get; private set; } = Vector3.Zero;

    /// <summary>
    /// Shared per-pose contract, enforced on every surface a pose can reach (a roster via
    /// <see cref="AttachPoseSet.ValidatedPoses"/> and the rider's <c>DefaultPose</c>): a pose with no Id
    /// can never be booked by a host, and a pose missing a clip name plays nothing while it still rides.
    /// </summary>
    public void Validate()
    {
        if (this.Id.ToString().Length == 0)
        {
            throw new ResourceConfigurationException(
                "An AttachPose has no Id — a host books poses by Id, so an unnamed pose can never be tracked.", this);
        }

        if (this.RideAnimationName.ToString().Length == 0 || this.AttackAnimationName.ToString().Length == 0)
        {
            throw new ResourceConfigurationException(
                $"AttachPose '{this.Id}' is missing a clip name (ride and attack are both required).", this);
        }
    }

    #region Test Helpers
#if TOOLS

    internal void SetPose(StringName id, StringName rideAnimationName, StringName attackAnimationName)
    {
        this.Id = id;
        this.RideAnimationName = rideAnimationName;
        this.AttackAnimationName = attackAnimationName;
    }

    internal void SetAttackAnimationHoldSeconds(float seconds) => this.AttackAnimationHoldSeconds = seconds;

    internal void SetPoseOffset(Vector3 offset) => this.PoseOffset = offset;

#endif
    #endregion
}
