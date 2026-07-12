namespace Jmodot.Implementation.AI.Navigation.Synthesis;

using Godot;
using Core.AI.Navigation;

/// <summary>
/// Legacy synthesis: weighted vector-sum of every UNMASKED bin by its clamped effective score, then
/// normalize. Retained for ambient/crowd drift (where mushy averaging is fine) and for A/B comparison
/// against <see cref="ArgmaxSynthesisStrategy3D"/> during rollout. Stateless: the input state is
/// ignored and <see cref="SteeringSynthesisState.Empty"/> is returned. With DangerScale = 1 and no
/// masks it reproduces the retired interim blend bridge exactly.
/// </summary>
[GlobalClass, Tool]
public sealed partial class BlendSynthesisStrategy3D : SteeringSynthesisStrategy3D
{
    public override (Vector3 Direction, SteeringSynthesisState NewState) Synthesize(
        SteeringContextMap map, in SteeringSynthesisState state)
    {
        int n = map.Bins.Count;
        if (n == 0) { return (Vector3.Zero, SteeringSynthesisState.Empty); }

        // Degenerate case matches Argmax exactly: all masked -> least-danger bin (ties -> lowest index).
        if (map.AllMasked)
        {
            int least = LeastDangerBin(map);
            return (map.Bins[least], SteeringSynthesisState.Empty);
        }

        Vector3 acc = Vector3.Zero;
        for (int i = 0; i < n; i++)
        {
            if (map.HardMask[i]) { continue; }
            acc += map.Bins[i] * Mathf.Max(0f, map.EffectiveScore(i, DangerScale));
        }

        return acc.IsZeroApprox()
            ? (Vector3.Zero, SteeringSynthesisState.Empty)
            : (acc.Normalized(), SteeringSynthesisState.Empty);
    }
}
