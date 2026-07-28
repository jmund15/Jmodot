namespace Jmodot.Core.Visual;

using Godot;

/// <summary>
/// Maps a normalised 0..1 ratio to a magnitude, with an optional curve for non-linear response.
/// The one place the project expresses "this quantity responds to that depleting resource" — a
/// spell's visual scale against its own health, its damage against the same, a status shell's size
/// against its integrity.
/// </summary>
/// <remarks>
/// Deliberately unitless: the same shape reads as metres, as a damage multiplier, or as grid units
/// of an entity's height. Naming the units here is what produced three near-identical copies of
/// this arithmetic in the first place.
/// <para>
/// The endpoints are named for their POSITION in the ratio domain, not for their order. An
/// authoring where <see cref="ScaleAtZero"/> exceeds <see cref="ScaleAtOne"/> is a legitimate
/// inverted response — something that grows as its resource drains — and is honoured rather than
/// normalised. Min/Max names would have made the same authoring read as a typo.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class ScaleRatioProfile : Resource
{
    /// <summary>Magnitude at a ratio of 0 — a fully depleted resource.</summary>
    [Export] public float ScaleAtZero { get; private set; } = 0.3f;

    /// <summary>Magnitude at a ratio of 1 — a full resource.</summary>
    [Export] public float ScaleAtOne { get; private set; } = 1.0f;

    /// <summary>Optional remap of the ratio before the lerp. Unset is linear.</summary>
    [Export] public Curve? ScaleCurve { get; private set; }

    /// <summary>Magnitude for <paramref name="ratio"/>, interpolated between the two endpoints.</summary>
    public float Evaluate(float ratio)
    {
        // Both clamps are load-bearing and guard different inputs. The first: a depleting resource
        // drives its ratio negative for the frame before its owner reacts. The second: Curve.Sample
        // honours the curve's own Min/MaxValue, which a designer can drag outside 0..1 and an
        // overshooting ease legitimately does — without it the result leaves the authored endpoints.
        float clamped = Mathf.Clamp(ratio, 0f, 1f);
        float t = Mathf.Clamp(ScaleCurve?.Sample(clamped) ?? clamped, 0f, 1f);

        return Mathf.Lerp(ScaleAtZero, ScaleAtOne, t);
    }

    #region Test Helpers
#if TOOLS
    internal void SetTestValues(float scaleAtZero, float scaleAtOne, Curve? curve = null)
    {
        ScaleAtZero = scaleAtZero;
        ScaleAtOne = scaleAtOne;
        ScaleCurve = curve;
    }
#endif
    #endregion
}
