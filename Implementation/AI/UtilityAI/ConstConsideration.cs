// --- ConstConsideration.cs ---
namespace JmoAI.UtilityAI;

using Godot;
using Jmodot.Core.AI.BB;

/// <summary>
/// Returns a constant utility score. Useful as a baseline or default action.
/// </summary>
[GlobalClass, Tool]
public partial class ConstConsideration : UtilityConsideration
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ConstValue { get; set; } = 0.5f;

    protected override float CalculateBaseScore(IBlackboard context)
    {
        return ConstValue;
    }
}
