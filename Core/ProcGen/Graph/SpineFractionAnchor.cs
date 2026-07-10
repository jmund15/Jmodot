namespace Jmodot.Core.ProcGen.Graph;

using System;
using Godot;

/// <summary>
///     A relative <see cref="PinAnchor" />: pins to a FRACTION of the spine actually drawn —
///     "halfway through this floor" survives any spine-length roll, where a fixed
///     <see cref="SpineIndexAnchor" /> would drift. Resolution rounds away from zero on even
///     spines (a 6-node spine's 0.5 resolves to index 3), biasing "halfway" toward the sink side.
/// </summary>
[GlobalClass, Tool]
public sealed partial class SpineFractionAnchor : PinAnchor
{
    /// <summary>Position along the spine as a fraction: 0 = source, 1 = sink.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Fraction { get; private set; }

    public override int ResolveSpineIndex(ISkeletonConfig config, int drawnSpineLength)
    {
        if (drawnSpineLength <= 1)
        {
            return 0;
        }

        return (int)Math.Round(this.Fraction * (drawnSpineLength - 1), MidpointRounding.AwayFromZero);
    }

    #region Test Helpers
#if TOOLS
    internal void SetFraction(float value) => this.Fraction = value;
#endif
    #endregion
}
