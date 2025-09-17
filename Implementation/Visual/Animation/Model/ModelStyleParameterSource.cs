namespace Jmodot.Implementation.Visual.Animation.Model;

// Jmodot/Implementation/Visual/Animation/Model/Parameters/StyleParameterSource3D.cs
using Godot;
using Godot.Collections;
using System.Linq;
using Jmodot.Core.Visual.Animation.Model;

/// <summary>
/// A resource that maps a simple string style (e.g., "unarmed", "rifle") to a specific
/// parameter value in the AnimationTree. This is useful for driving blend nodes or booleans
/// that switch between different animation sets (e.g., idle with rifle vs. idle without).
/// </summary>
[GlobalClass]
public partial class ModelStyleParameterSource : ModelAnimParameterSource
{
    [Export] public StringName ParameterName { get; set; }
    [Export] public Dictionary<string, Variant> StyleMappings { get; set; } = new();

    private Variant _currentValue;

    public ModelStyleParameterSource()
    {
        // Initialize with the first style if available to prevent null parameter values.
        if (StyleMappings.Count > 0)
        {
            SetStyle(StyleMappings.Keys.First());
        }
    }

    /// <summary>
    /// Caches the parameter value corresponding to the given style name.
    /// </summary>
    public void SetStyle(string styleName)
    {
        if (StyleMappings.TryGetValue(styleName, out Variant value))
        {
            _currentValue = value;
        }
    }

    /// <summary>
    /// Applies the cached style value to the AnimationTree controller.
    /// </summary>
    public override void UpdateParameters(IAnim3DController controller)
    {
        if (!ParameterName.IsEmpty)
        {
            controller.SetParameter(ParameterName, _currentValue);
        }
    }
}
