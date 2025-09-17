namespace Jmodot.Core.Visual.Animation.Model;

using Godot;
using Shared;

/// <summary>
/// Defines the contract for controlling a 3D animation system, typically an AnimationTree.
/// This interface abstracts away the direct implementation, focusing on setting parameters
/// and triggering state transitions, which is the standard paradigm for 3D animation.
/// </summary>
public interface IAnim3DController : IGodotNodeInterface
{
    void SetParameter(StringName path, Variant value);
    Variant GetParameter(StringName path);
    void TriggerOneShot(StringName stateName);
    void TravelToState(StringName stateName);
}
