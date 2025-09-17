namespace Jmodot.Core.Visual.Animation.Model;

using Godot;

/// <summary>
/// Abstract base class for all model animation parameter sources.
/// These are data-driven Resources that are responsible for updating specific
/// parameters on an IAnim3DController (like an AnimationTree) based on game state.
/// </summary>
[GlobalClass]
public abstract partial class ModelAnimParameterSource : Resource
{
    /// <summary>
    /// Called by the AnimationOrchestrator3D every frame to update the
    /// relevant animation parameters on the target controller.
    /// </summary>
    /// <param name="controller">The animation controller to update.</param>
    public abstract void UpdateParameters(IAnim3DController controller);
}
