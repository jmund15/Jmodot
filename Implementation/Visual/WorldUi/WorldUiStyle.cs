namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using Godot;

/// <summary>
/// The look of one world-UI role: element heights, the gap to the art, the depth mode, the show/hide
/// transition and the scene that draws text. A <see cref="WorldUiPrompt"/> draws with one style.
/// </summary>
/// <remarks>
/// A length that is 0, negative or not finite reads as its framework fallback in <see cref="WorldUiScale"/>; the
/// owning prompt names it in its configuration warnings through <see cref="Faults"/>.
/// </remarks>
[GlobalClass, Tool]
public partial class WorldUiStyle : Resource
{
    /// <summary>Render priority of an always-on-top element. Sorted elements draw at 0.</summary>
    public const int OnTopRenderPriority = 8;

    private static WorldUiStyle? _fallback;

    private float _textWorldHeight = WorldUiScale.FallbackTextWorldHeightMetres;
    private float _iconWorldHeight = WorldUiScale.FallbackIconWorldHeightMetres;
    private float _anchorPadding = WorldUiScale.FallbackAnchorPaddingMetres;

    /// <summary>The style used when no style is authored. Created on first use.</summary>
    public static WorldUiStyle Fallback => _fallback ??= new WorldUiStyle();

    /// <summary>Em height, in metres, of text drawn with this style.</summary>
    [Export(PropertyHint.Range, "0,2,0.01,suffix:m")]
    public float TextWorldHeight
    {
        get => WorldUiScale.IsUsable(_textWorldHeight) ? _textWorldHeight : WorldUiScale.FallbackTextWorldHeightMetres;
        set => _textWorldHeight = value;
    }

    /// <summary>Height, in metres, of icons drawn with this style. Independent of <see cref="TextWorldHeight"/>.</summary>
    [Export(PropertyHint.Range, "0,2,0.01,suffix:m")]
    public float IconWorldHeight
    {
        get => WorldUiScale.IsUsable(_iconWorldHeight) ? _iconWorldHeight : WorldUiScale.FallbackIconWorldHeightMetres;
        set => _iconWorldHeight = value;
    }

    /// <summary>Screen gap, in metres, between the art and the nearest element, and between stacked elements.</summary>
    [Export(PropertyHint.Range, "0,1,0.01,suffix:m")]
    public float AnchorPadding
    {
        get => WorldUiScale.IsUsable(_anchorPadding) ? _anchorPadding : WorldUiScale.FallbackAnchorPaddingMetres;
        set => _anchorPadding = value;
    }

    /// <summary>Whether elements sort with the world or draw on top of it.</summary>
    [Export]
    public WorldUiDepthMode DepthMode { get; set; } = WorldUiDepthMode.SortedWithWorld;

    /// <summary>How the prompt shows and hides. Null snaps.</summary>
    [Export]
    public WorldUiTransition? Transition { get; set; }

    /// <summary>The scene whose root is a <see cref="WorldUiLabel"/>; a row instances it for its text.</summary>
    [Export]
    public PackedScene? LabelScene { get; set; }

    /// <summary>
    /// One explanation per length that is unusable and so reads its fallback. Empty when every length is usable.
    /// </summary>
    public IReadOnlyList<string> Faults()
    {
        var faults = new List<string>();
        if (!WorldUiScale.IsUsable(_textWorldHeight))
        {
            faults.Add(WorldUiScale.UnusableLengthExplanation("Text World Height", _textWorldHeight, WorldUiScale.FallbackTextWorldHeightMetres));
        }

        if (!WorldUiScale.IsUsable(_iconWorldHeight))
        {
            faults.Add(WorldUiScale.UnusableLengthExplanation("Icon World Height", _iconWorldHeight, WorldUiScale.FallbackIconWorldHeightMetres));
        }

        if (!WorldUiScale.IsUsable(_anchorPadding))
        {
            faults.Add(WorldUiScale.UnusableLengthExplanation("Anchor Padding", _anchorPadding, WorldUiScale.FallbackAnchorPaddingMetres));
        }

        return faults;
    }
}
