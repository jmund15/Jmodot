namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// A world-UI text element (<see cref="IWorldUiElement"/>): a <see cref="Label3D"/> whose em height comes from
/// the <see cref="WorldUiStyle"/> it is given. <c>pixel_size</c> is derived from that height and the inherited
/// <c>font_size</c>, so no scene authors it.
/// </summary>
/// <remarks>
/// <para>
/// Billboarding, depth test and render priority come from the applied style (<see cref="ApplyStyle"/>). An
/// unstyled label uses <see cref="WorldUiStyle.Fallback"/>.
/// </para>
/// <para>
/// <c>pixel_size</c> is re-derived when the style is applied, when <c>font_size</c> changes, when a scene instance
/// finishes loading, at <see cref="_Ready"/> and on editor pre-save. A change to <c>text</c>, <c>font_size</c> or
/// <c>font</c> made through this type's members takes effect at once; one made any other way is picked up on
/// this label's next processed frame. Either way it raises <see cref="LayoutChanged"/> once.
/// </para>
/// <para>
/// The drawn box must stay one line centred on the origin (<see cref="IWorldUiElement"/>). A non-centre
/// alignment, a non-zero <c>offset</c>, autowrap or a newline in the text raises a configuration warning.
/// </para>
/// <para>
/// A character the face cannot draw renders as a missing-character box. The label checks its text with
/// <see cref="MissingGlyphs"/> whenever the text or face is set through this type and at <see cref="_Ready"/>:
/// the dock lists the missing characters, and at runtime one <c>Warning</c> is logged per distinct face
/// and missing set for the whole process, however many labels print it.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class WorldUiLabel : Label3D, IWorldUiElement
{
    /// <summary>
    /// Raised once per change of the applied style, <c>text</c>, <c>font_size</c> or <c>font</c>: at once through this
    /// type's members, and on the next processed frame for a write that bypasses them.
    /// </summary>
    public event System.Action LayoutChanged = delegate { };

    /// <summary>Face and missing set already warned about; process-wide because a row rebuilds its label on every text change.</summary>
    private static readonly HashSet<(ulong FontId, string Missing)> WarnedGlyphSets = new();

    private WorldUiStyle? _style;
    private (string Text, int FontSize, Font? Font) _observed = (string.Empty, 0, null);

    private WorldUiStyle Style => _style ?? WorldUiStyle.Fallback;

    /// <summary>
    /// The distinct characters of <paramref name="text"/> that <paramref name="font"/> cannot draw, in order
    /// of first appearance. Whitespace draws nothing, so it is never reported.
    /// </summary>
    public static IReadOnlyList<char> MissingGlyphs(Font font, string text)
        => text.Where(c => !char.IsWhiteSpace(c) && !font.HasChar(c)).Distinct().ToList();

    /// <summary>The displayed text. A new value set through this type raises <see cref="LayoutChanged"/> at once.</summary>
    public new string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            ObserveDrawnState();
        }
    }

    /// <summary>Raster resolution. A new value set through this type re-derives <c>pixel_size</c> and raises <see cref="LayoutChanged"/> at once.</summary>
    public new int FontSize
    {
        get => base.FontSize;
        set
        {
            base.FontSize = value;
            ObserveDrawnState();
        }
    }

    /// <summary>The face. A new face set through this type raises <see cref="LayoutChanged"/> at once, since line height is per face.</summary>
    public new Font? Font
    {
        get => base.Font;
        set
        {
            base.Font = value;
            ObserveDrawnState();
        }
    }

    /// <summary>
    /// The height one line of this label actually DRAWS, in metres — what a caller must clear to sit clear of it.
    /// It is the face's line height (<c>Font.get_height</c>) times <c>pixel_size</c>, so it follows any font,
    /// including the engine fallback face when <c>Font</c> is null. It is taller than the em.
    /// </summary>
    public float RenderedHeight => DrawnFont.GetHeight(FontSize) * PixelSize;

    /// <summary>The face this label draws with: its own, or the engine fallback face when none is set.</summary>
    private Font DrawnFont => Font ?? ThemeDB.FallbackFont;

    /// <summary>Writes <paramref name="alpha"/> to the text and its outline, leaving their colours.</summary>
    public void SetPresentationAlpha(float alpha)
    {
        Modulate = new Color(Modulate, alpha);
        OutlineModulate = new Color(OutlineModulate, alpha);
    }

    /// <summary>
    /// Applies the style's em height, depth test and render priority, billboarding, then raises
    /// <see cref="LayoutChanged"/>.
    /// </summary>
    public void ApplyStyle(WorldUiStyle style)
    {
        _style = style;
        ApplyStyleState();
        LayoutChanged();
    }

    public override void _Ready()
    {
        WarnOnceForMissingGlyphs();
        ApplyStyleState();
        _observed = (base.Text, base.FontSize, base.Font);
    }

    /// <summary>
    /// Picks up a <c>text</c>, <c>font_size</c> or <c>font</c> write that bypassed this type's members.
    /// </summary>
    public override void _Process(double delta) => ObserveDrawnState();

    /// <summary>
    /// Re-derives on editor save, and once a scene instance is loaded: a scene that authors no style sets nothing that
    /// re-derives, so an instance measured before <see cref="_Ready"/> would otherwise carry <c>Label3D</c>'s default
    /// <c>pixel_size</c>.
    /// </summary>
    public override void _Notification(int what)
    {
        if (what == NotificationEditorPreSave || what == NotificationSceneInstantiated)
        {
            ApplyDerivedPixelSize();
        }
    }

    /// <summary>
    /// Removes the derived <c>pixel_size</c> and the style-driven <c>no_depth_test</c>, <c>render_priority</c> and
    /// <c>billboard</c> from the authoring surface completely. Usage is cleared to <c>None</c>, not set to
    /// <c>ReadOnly | NoEditor</c>, because in Godot 4.7 <c>PROPERTY_USAGE_NO_EDITOR</c> and
    /// <c>PROPERTY_USAGE_STORAGE</c> are the same value and a hidden property would keep serialising.
    /// </summary>
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        StringName name = property["name"].AsStringName();
        if (name == Label3D.PropertyName.PixelSize || name == Label3D.PropertyName.NoDepthTest
            || name == Label3D.PropertyName.RenderPriority || name == Label3D.PropertyName.Billboard)
        {
            property["usage"] = (int)PropertyUsageFlags.None;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (FontSize <= 0)
        {
            warnings.Add(
                $"Font Size is {FontSize}. The metres-to-pixel_size derivation needs a positive raster " +
                $"resolution; it is falling back to {WorldUiScale.FallbackFontSize}.");
        }

        if (Font is null)
        {
            warnings.Add(
                "No Font assigned. This label falls back to the engine default face, not the project " +
                "pixel font, and will not match any other world-space text.");
        }

        IReadOnlyList<char> missing = MissingGlyphs(DrawnFont, base.Text);
        if (missing.Count > 0)
        {
            warnings.Add($"The font cannot draw {DescribeGlyphs(missing)}; each draws as a missing-character box.");
        }

        warnings.AddRange(PlacementFaults());
        return warnings.ToArray();
    }

    private static string DescribeGlyphs(IEnumerable<char> glyphs)
        => string.Join(", ", glyphs.Select(c => $"'{c}' (U+{(int)c:X4})"));

    private void WarnOnceForMissingGlyphs()
    {
        if (Engine.IsEditorHint()) { return; }

        Font font = DrawnFont;
        IReadOnlyList<char> missing = MissingGlyphs(font, base.Text);
        if (missing.Count == 0 || !WarnedGlyphSets.Add((font.GetInstanceId(), new string(missing.ToArray())))) { return; }

        string face = string.IsNullOrEmpty(font.ResourcePath) ? font.GetClass() : font.ResourcePath;
        JmoLogger.Warning(this, $"[WorldUi] {Name}: {face} cannot draw {DescribeGlyphs(missing)} in \"{base.Text}\"; each draws as a missing-character box.");
    }

    /// <summary>
    /// The one change path for what the label draws: compares <c>text</c>, <c>font_size</c> and <c>font</c> with
    /// what it last saw and, on any difference, re-derives <c>pixel_size</c>, re-checks glyphs and warnings and
    /// raises <see cref="LayoutChanged"/> once.
    /// </summary>
    private void ObserveDrawnState()
    {
        (string Text, int FontSize, Font? Font) now = (base.Text, base.FontSize, base.Font);
        if (now.Text == _observed.Text && now.FontSize == _observed.FontSize && ReferenceEquals(now.Font, _observed.Font)) { return; }

        _observed = now;
        ApplyDerivedPixelSize();
        WarnOnceForMissingGlyphs();
        UpdateConfigurationWarnings();
        LayoutChanged();
    }

    /// <summary>Authored properties that move the drawn box off the origin or add a line, each of which breaks prompt placement.</summary>
    private IEnumerable<string> PlacementFaults()
    {
        if (HorizontalAlignment != HorizontalAlignment.Center)
        {
            yield return $"Horizontal Alignment is {HorizontalAlignment}; a world-UI label is centred on its origin, so the prompt misplaces it.";
        }

        if (VerticalAlignment != VerticalAlignment.Center)
        {
            yield return $"Vertical Alignment is {VerticalAlignment}; a world-UI label is centred on its origin, so the prompt misplaces it.";
        }

        if (Offset != Vector2.Zero)
        {
            yield return $"Offset is {Offset}; it moves the drawn text off the origin the prompt places.";
        }

        if (AutowrapMode != TextServer.AutowrapMode.Off)
        {
            yield return $"Autowrap Mode is {AutowrapMode}; a world-UI label is one line, and a wrapped line overlaps what the prompt stacks beside it.";
        }

        if (base.Text.Contains('\n'))
        {
            yield return "The text has a newline; a world-UI label is one line, and a second line overlaps what the prompt stacks beside it.";
        }
    }

    private void ApplyStyleState()
    {
        WorldUiStyle style = Style;
        NoDepthTest = style.NoDepthTest;
        RenderPriority = style.RenderPriority;
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        ApplyDerivedPixelSize();
    }

    private void ApplyDerivedPixelSize()
        => PixelSize = WorldUiScale.PixelSizeForFont(Style.TextWorldHeight, FontSize);

    #region Test Helpers
#if TOOLS
    internal static void ResetGlyphWarningsForTest() => WarnedGlyphSets.Clear();
#endif
    #endregion
}
