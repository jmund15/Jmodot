namespace Jmodot.Implementation.Visual.Animation.Model;

using Godot;
using Godot.Collections;
using System.Linq;
using Core.Visual.Animation.Model;

/// <summary>
/// The central controller for the model-based animation system. It does not build names,
/// but instead updates a collection of AnimationParameterSource resources every frame,
/// which in turn drive the parameters of a target IAnim3DController.
/// </summary>
[GlobalClass]
public partial class ModelAnimationOrchestrator : Node
{
    [Export] private NodePath _targetControllerPath;
    [Export] public Array<ModelAnimParameterSource> ParameterSources { get; set; } = new();

    private IAnim3DController _targetController;

    public override void _Ready()
    {
        _targetController = GetNode<Node>(_targetControllerPath) as IAnim3DController;
        if (_targetController == null)
        {
            GD.PrintErr($"Orchestrator3D '{Name}': Target at '{_targetControllerPath}' is not a valid IAnim3DController.");
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        // Every frame, update all registered parameter sources.
        foreach (var source in ParameterSources)
        {
            source.UpdateParameters(_targetController);
        }
    }

    /// <summary>
    /// Provides new locomotion input to all registered LocomotionParameterSource resources.
    /// </summary>
    public void SetLocomotionInput(Vector3 worldVelocity, Vector3 modelForward)
    {
        foreach (var source in ParameterSources.OfType<LocomotionParameterSource>())
        {
            source.SetInput(worldVelocity, modelForward);
        }
    }

    /// <summary>
    /// Provides a new style to all registered StyleParameterSource3D resources.
    /// </summary>
    public void SetStyle(string styleName)
    {
        foreach (var source in ParameterSources.OfType<ModelStyleParameterSource>())
        {
            source.SetStyle(styleName);
        }
    }

    public void PlayState(StringName stateName) => _targetController.TravelToState(stateName);
    public void FireAction(StringName actionName) => _targetController.TriggerOneShot(actionName);
}
