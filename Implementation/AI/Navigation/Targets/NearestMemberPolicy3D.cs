namespace Jmodot.Implementation.AI.Navigation.Targets;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Keeps the single closest eligible member. Ties break on the lowest instance ID, so two members
/// at the same distance always resolve the same one regardless of perception order.
/// </summary>
[GlobalClass, Tool]
public partial class NearestMemberPolicy3D : TargetPolicy3D
{
    private const float TieTolerance = 0.0001f;

    public override TargetSampleGeometry Geometry => TargetSampleGeometry.Member;

    public override void Apply(TargetSample sample)
    {
        var members = sample.Members;
        if (members.Count == 0) { return; }

        var best = members[0];
        for (var i = 1; i < members.Count; i++)
        {
            var candidate = members[i];
            if (candidate.FlatDistance < best.FlatDistance - TieTolerance)
            {
                best = candidate;
                continue;
            }

            if (Mathf.Abs(candidate.FlatDistance - best.FlatDistance) <= TieTolerance
                && candidate.InstanceId < best.InstanceId)
            {
                best = candidate;
            }
        }

        sample.KeepOnly(best);
        sample.Seal(Geometry);
    }
}
