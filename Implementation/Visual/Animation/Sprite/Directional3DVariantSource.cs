namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Core.Visual.Animation.Sprite;
using Core.Movement;
using Godot;
using Godot.Collections;
using Shared;

/// <summary>
/// A resource that provides an animation name variant based on a continuous 3D direction,
/// snapped to the closest direction defined in a DirectionSet3D resource.
/// </summary>
/// <remarks>
/// KNOWN RISK: Instances of this Resource are typically shared as .tres files.
/// The mutable field (_currentVariant) holds per-instance state on a shared Resource.
/// This works because Godot loads unique copies per scene, but can cause issues if
/// Resources are explicitly shared via code (Resource.LocalToScene or manual assignment).
/// If sharing becomes a problem, implement IRuntimeCopyable or use a separate runtime state object.
/// </remarks>
[GlobalClass]
public partial class Directional3DVariantSource : AnimVariantSource
{
    [Export] public DirectionSet3D DirectionSet { get; set; }
    [Export] public Dictionary<Vector3, string> DirectionLabels { get; set; } = new();

    private string _currentVariant = "";

    /// <summary>
    /// Called by the AnimationOrchestrator to update the internal directional state.
    /// </summary>
    public void UpdateDirection(Vector3 newDirection)
    {
        //JmoLogger.Info(this, $"updating direction for anim style. new dir: {newDirection}");
        if (DirectionSet == null || newDirection.IsZeroApprox())
        {
            //_currentVariant = "";
            return;
        }

        Vector3 closestDir = DirectionSet.GetClosestDirection(newDirection.Normalized());
        _currentVariant = DirectionLabels.TryGetValue(closestDir, out string label) ? label : "";
        // JmoLogger.Info(this, $"closest dir: {closestDir}" +
        //                      $"\nvariant label: '{_currentVariant}'");

    }

    public override string GetAnimVariant() => _currentVariant;
}
