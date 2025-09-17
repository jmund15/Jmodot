namespace Jmodot.Implementation.Visual.Animation.Model;

using Godot;
using Core.Visual.Animation.Model;

/// <summary>
/// A concrete implementation of IAnim3DController that wraps Godot's built-in AnimationTree node.
/// This is the primary node for driving 3D model animations.
/// </summary>
[GlobalClass]
public partial class AnimationTreeController : AnimationTree, IAnim3DController
{
    private const string PARAMETER_BASE_PATH = "parameters/";

    public void SetParameter(StringName path, Variant value) => Set(PARAMETER_BASE_PATH + path, value);
    public Variant GetParameter(StringName path) => Get(PARAMETER_BASE_PATH + path);

    public void TriggerOneShot(StringName stateName)
        => SetParameter(stateName + "/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);

    public void TravelToState(StringName stateName)
        => (TreeRoot as AnimationNodeStateMachinePlayback)?.Travel(stateName);

    public Node GetUnderlyingNode() => this;
}
