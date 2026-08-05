namespace Jmodot.Implementation.Interaction.Attachment;

using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared.GodotExceptions;

/// <summary>
/// The full roster of <see cref="AttachPose"/>s one rider's art provides. Authored on the RIDER,
/// because the poses are its own art: the same set works on every host, and the number of poses is
/// the parallel-ride limit that binds before any host's mechanical capacity does.
///
/// <para>
/// <b>Stateless.</b> One <c>.tres</c> is shared by every rider that authors it, so nothing about who
/// currently holds which pose may live here — occupancy is per host, keyed by
/// <see cref="AttachPose.Id"/> against that host's own records.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class AttachPoseSet : Resource
{
    /// <summary>Every pose this rider's art provides. Ids must be unique and every clip name authored.</summary>
    [Export] public AttachPose[] Poses { get; private set; } = [];

    private bool _validated;

    /// <summary>
    /// The poses, validated on first use: a set with no poses, a duplicate or empty
    /// <see cref="AttachPose.Id"/>, or an unauthored clip name throws
    /// <see cref="ResourceConfigurationException"/> here rather than degrading into riders that
    /// silently share a pose or play nothing. Validation runs once per instance; consumers read the
    /// roster through this property so no path can reach the raw export unchecked.
    /// </summary>
    public IReadOnlyList<AttachPose> ValidatedPoses
    {
        get
        {
            if (!this._validated) { this.Validate(); }
            return this.Poses;
        }
    }

    private void Validate()
    {
        if (this.Poses.Length == 0)
        {
            throw new ResourceConfigurationException(
                "An AttachPoseSet authors no poses, so every rider carrying it would be refused by every host.", this);
        }

        var seen = new HashSet<string>();
        foreach (var pose in this.Poses)
        {
            if (pose == null)
            {
                throw new ResourceConfigurationException("An AttachPoseSet holds an empty pose slot.", this);
            }

            pose.Validate();

            if (!seen.Add(pose.Id.ToString()))
            {
                throw new ResourceConfigurationException(
                    $"AttachPose Id '{pose.Id}' appears twice. Occupancy is keyed by Id, so duplicates would let two " +
                    "riders hold the same visual.", this);
            }
        }

        this._validated = true;
    }

    #region Test Helpers
#if TOOLS

    internal void SetPoses(AttachPose[] poses)
    {
        this.Poses = poses;
        this._validated = false;
    }

#endif
    #endregion
}
