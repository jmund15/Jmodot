namespace Jmodot.Implementation.Visual.WorldUi;

using Godot;

/// <summary>
/// The look of one world-UI role: element heights and the gap to the art, in metres. A
/// <see cref="WorldUiPrompt"/> draws with one style.
/// </summary>
[GlobalClass, Tool]
public partial class WorldUiStyle : Resource
{
    /// <summary>
    /// Interim house value of <see cref="TextWorldHeight"/> and of a label's default em height. Interim
    /// until styles are authored resources; the value is the house text height.
    /// </summary>
    public const float InterimTextWorldHeight = 0.19f;

    /// <summary>Interim house value of <see cref="IconWorldHeight"/> and of a row's default icon height.</summary>
    public const float InterimIconWorldHeight = 0.38f;

    /// <summary>Interim house value of <see cref="AnchorPadding"/> and of a prompt's default gap.</summary>
    public const float InterimAnchorPadding = 0.08f;

    /// <summary>Em height, in metres, of text drawn with this style.</summary>
    [Export(PropertyHint.Range, "0,2,0.01,suffix:m")]
    public float TextWorldHeight { get; set; } = InterimTextWorldHeight;

    /// <summary>Height, in metres, of icons drawn with this style. Independent of <see cref="TextWorldHeight"/>.</summary>
    [Export(PropertyHint.Range, "0,2,0.01,suffix:m")]
    public float IconWorldHeight { get; set; } = InterimIconWorldHeight;

    /// <summary>Screen gap, in metres, between the art and the nearest element, and between stacked elements.</summary>
    [Export(PropertyHint.Range, "0,1,0.01,suffix:m")]
    public float AnchorPadding { get; set; } = InterimAnchorPadding;
}
