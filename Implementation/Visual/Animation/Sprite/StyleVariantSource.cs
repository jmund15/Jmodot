namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using Godot.Collections;
using Core.Visual.Animation.Sprite;

/// <summary>
/// A resource that provides a simple string-based variant for animation,
/// ideal for representing equipment styles (e.g., "sword", "bow") or character states.
/// </summary>
/// <remarks>
/// KNOWN RISK: Instances of this Resource are typically shared as .tres files.
/// The mutable field (_currentVariant) holds per-instance state on a shared Resource.
/// This works because Godot loads unique copies per scene, but can cause issues if
/// Resources are explicitly shared via code (Resource.LocalToScene or manual assignment).
/// If sharing becomes a problem, implement IRuntimeCopyable or use a separate runtime state object.
/// </remarks>
[GlobalClass]
public partial class StyleVariantSource : AnimVariantSource
{
    [Export] private Array<string> _styleOptions = new();

    private string _currentVariant = "";

    public StyleVariantSource()
    {
        // Initialize with the first style if available.
        if (_styleOptions.Count > 0)
        {
            _currentVariant = _styleOptions[0];
        }
    }

    /// <summary>
    /// Called by the AnimationOrchestrator to update the active style.
    /// </summary>
    public void UpdateStyle(string styleName)
    {
        if (_styleOptions.Contains(styleName))
        {
            _currentVariant = styleName;
        }
    }
    public override string GetAnimVariant() => _currentVariant;
}
