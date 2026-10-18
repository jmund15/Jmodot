namespace Jmodot.Implementation.AI.Navigation.Targets;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Keeps every eligible member as a set and owns the two numbers that shape how a response over
/// that set is combined: resolution and focus. Includes members that share the agent's position.
/// </summary>
[GlobalClass, Tool]
public partial class AllMembersPolicy3D : TargetPolicy3D
{
    /// <summary>
    /// How per-member responses combine. 0 sums the member vectors before scoring, so opposing
    /// threats can cancel; 1 scores each member and averages, so gaps between them survive.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float Resolution { get; private set; }

    /// <summary>
    /// How attention concentrates. 0 weighs every member equally; 1 keeps only the dominant
    /// member's influence, so lesser threats fade out.
    /// </summary>
    [Export(PropertyHint.Range, "0.0, 1.0, 0.05")]
    public float Focus { get; private set; }

    public override TargetSampleGeometry Geometry => TargetSampleGeometry.Set;

    public override void Apply(TargetSample sample)
        => sample.Seal(Geometry, Resolution, Focus);

    public override string[] GetResourceConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (!IsUnitRange(Resolution))
        {
            warnings.Add($"Resolution must be a finite number between 0 and 1 (it is {Resolution}).");
        }

        if (!IsUnitRange(Focus))
        {
            warnings.Add($"Focus must be a finite number between 0 and 1 (it is {Focus}).");
        }

        return warnings.ToArray();
    }

    private static bool IsUnitRange(float value) => float.IsFinite(value) && value >= 0f && value <= 1f;

    #region Test Helpers
#if TOOLS
    internal void SetResolutionForTest(float resolution) => Resolution = resolution;
    internal void SetFocusForTest(float focus) => Focus = focus;
#endif
    #endregion
}
