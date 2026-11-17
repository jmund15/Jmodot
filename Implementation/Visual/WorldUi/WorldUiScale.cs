namespace Jmodot.Implementation.Visual.WorldUi;

/// <summary>
/// Derives a world-space node's <c>pixel_size</c> from a height authored in METRES.
///
/// <para>
/// There are two methods rather than one because the unit being converted FROM is exactly what gets
/// conflated. A font's source measure is <c>font_size</c> in em-pixels; a sprite's is its texture
/// height in real pixels. Sharing one constant across both is the defect this class exists to make
/// unauthorable.
/// </para>
/// </summary>
public static class WorldUiScale
{
    /// <summary>Framework fallback em height, in metres, for a text element with no usable authored height.</summary>
    public const float FallbackTextWorldHeightMetres = 0.25f;

    /// <summary>Framework fallback icon height, in metres, for an icon with no usable authored height.</summary>
    public const float FallbackIconWorldHeightMetres = 0.5f;

    /// <summary>Framework fallback screen gap, in metres, between world-UI elements and the art they bracket.</summary>
    public const float FallbackAnchorPaddingMetres = 0.1f;

    /// <summary>Font size, in em-pixels, assumed when a label's own font size is not positive.</summary>
    public const int FallbackFontSize = 64;

    /// <summary>Texture height, in pixels, assumed when a texture reports none: one metre at the project's 16 px/m sprite encoding.</summary>
    public const int FallbackTextureHeightPx = 16;

    /// <summary>
    /// Whether an authored world-UI length in metres can be used as is. A length that is not finite, or is
    /// <c>0</c> or less, is unusable: <c>0</c> means authored as 0 or null-stripped by an editor resave, and
    /// every consumer renders its fallback instead.
    /// </summary>
    public static bool IsUsable(float metres) => float.IsFinite(metres) && metres > 0f;

    /// <summary>
    /// The one explanation of an unusable length, for the dock warning and the runtime log alike.
    /// <paramref name="property"/> is the Inspector name and <paramref name="houseMetres"/> the value
    /// the consumer renders instead.
    /// </summary>
    public static string UnusableLengthExplanation(string property, float value, float houseMetres)
        => $"{property} is {value} m: authored as 0, or null-stripped by a Godot editor resave (a value-type export " +
            "equal to its C# default is written as null and reloads as 0). Reset it to the default or author a positive " +
            $"value; the {houseMetres} m fallback is used instead.";

    /// <summary>
    /// The <c>pixel_size</c> that draws a font's em at <paramref name="targetWorldHeightMetres"/>. An unusable
    /// height falls back to <paramref name="fallbackHeightMetres"/>.
    /// </summary>
    public static float PixelSizeForFont(
        float targetWorldHeightMetres, int fontSize, float fallbackHeightMetres = WorldUiStyle.InterimTextWorldHeight)
        => Derive(targetWorldHeightMetres, fallbackHeightMetres, fontSize, FallbackFontSize);

    /// <summary>
    /// The <c>pixel_size</c> that draws a texture at <paramref name="targetWorldHeightMetres"/>. An unusable
    /// height falls back to <paramref name="fallbackHeightMetres"/>.
    /// </summary>
    public static float PixelSizeForTexture(
        float targetWorldHeightMetres, int textureHeightPx, float fallbackHeightMetres = WorldUiStyle.InterimIconWorldHeight)
        => Derive(targetWorldHeightMetres, fallbackHeightMetres, textureHeightPx, FallbackTextureHeightPx);

    /// <summary>
    /// Total by construction: a NaN or Infinity <c>pixel_size</c> renders an invisible node with no
    /// error, so every unusable input falls back rather than propagating. The loud half of the contract
    /// belongs to the caller, because rendering garbage to force attention is the hostile choice.
    /// An unusable divisor keeps the authored height, which is the one input still worth trusting.
    /// </summary>
    private static float Derive(float heightMetres, float fallbackHeightMetres, int sourcePixels, int fallbackSourcePixels)
    {
        float height = IsUsable(heightMetres) ? heightMetres : fallbackHeightMetres;
        int divisor = sourcePixels > 0 ? sourcePixels : fallbackSourcePixels;

        return height / divisor;
    }
}
