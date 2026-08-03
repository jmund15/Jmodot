namespace Jmodot.Implementation.Interaction.Attachment;

using Godot;

/// <summary>
/// One attach VISUAL a rider can occupy on a host: an identity plus the two clips its art plays
/// while it is held. Pose art is drawn on a host-sized canvas with the rider already at the right
/// body spot, so the pose carries no offset — a rider holding one rides at the host's origin.
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

    #region Test Helpers
#if TOOLS

    internal void SetPose(StringName id, StringName rideAnimationName, StringName attackAnimationName)
    {
        this.Id = id;
        this.RideAnimationName = rideAnimationName;
        this.AttackAnimationName = attackAnimationName;
    }

#endif
    #endregion
}
