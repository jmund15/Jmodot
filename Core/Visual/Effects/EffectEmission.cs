namespace Jmodot.Core.Visual.Effects;

using System;
using Godot;

public static class EffectEmission
{
    public static readonly StringName Uniform = "effect_emission";
    public static readonly Color None = new(0f, 0f, 0f, 0f);

    /// <summary>Combines colours by alpha-weighted mean and weights by complementary product; non-finite channels read as zero.</summary>
    public static Color Combine(ReadOnlySpan<Color> emissions)
    {
        double remaining = 1d;
        double total = 0d;
        double red = 0d;
        double green = 0d;
        double blue = 0d;
        foreach (var emission in emissions)
        {
            float weight = Clamp(emission.A);
            remaining *= 1d - weight;
            total += weight;
            red += weight * Clamp(emission.R);
            green += weight * Clamp(emission.G);
            blue += weight * Clamp(emission.B);
        }
        if (total <= 0d) { return None; }
        return new Color((float)(red / total), (float)(green / total),
            (float)(blue / total), (float)(1d - remaining));
    }

    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
}
