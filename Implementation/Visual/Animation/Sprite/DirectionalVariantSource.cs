namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;
using Core.Movement;
using Godot;
using Godot.Collections;

/// <summary>
/// A resource that provides an animation name variant based on a continuous 3D direction,
/// snapped to the closest direction defined in a DirectionSet3D resource.
/// </summary>
[GlobalClass]
public partial class DirectionalVariantSource : AnimVariantSource
{
    [Export] public DirectionSet3D DirectionSet { get; set; }
    [Export] public Dictionary<Vector3, string> DirectionLabels { get; set; } = new();

    private string _currentVariant = "";

    /// <summary>
    /// Called by the AnimationOrchestrator to update the internal directional state.
    /// </summary>
    public void UpdateDirection(Vector3 newDirection)
    {
        if (DirectionSet == null || newDirection.IsZeroApprox())
        {
            _currentVariant = "";
            return;
        }

        Vector3 closestDir = DirectionSet.GetClosestDirection(newDirection.Normalized());
        _currentVariant = DirectionLabels.TryGetValue(closestDir, out string label) ? label : "";
    }

    public override string GetVariant() => _currentVariant;
}
