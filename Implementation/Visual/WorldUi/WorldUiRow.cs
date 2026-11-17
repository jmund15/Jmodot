namespace Jmodot.Implementation.Visual.WorldUi;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// A horizontal row of world-UI items (<see cref="IWorldUiElement"/>): icons from
/// <see cref="SetIcons(IReadOnlyList{ValueTuple{Texture2D, Color}})"/>, or one line of text from
/// <see cref="SetText"/>. Icons sit edge to edge,
/// each <see cref="IconWorldHeight"/> tall and as wide as its own aspect, the row centred on its origin.
/// Text draws a <see cref="WorldUiLabel"/> instanced from <see cref="LabelScene"/>, at that label's own height,
/// never at the icon height.
/// </summary>
/// <remarks>
/// The row owns every child it draws and rebuilds them on each call, so no caller can change an item
/// behind its back. It frees only the children it built: an authored child survives a rebuild and is
/// named in a configuration warning, since the row neither sizes nor places it. Built icons billboard,
/// draw with <c>no_depth_test</c>, nearest filtering and at <see cref="WorldUiLabel.WorldUiRenderPriority"/>;
/// text is a <see cref="WorldUiLabel"/> from the house label scene, which enforces the same itself.
/// <c>[Tool]</c> so its configuration warnings reach the scene dock.
/// </remarks>
[GlobalClass, Tool]
public partial class WorldUiRow : Node3D, IWorldUiElement
{
    private float _iconWorldHeight = WorldUiStyle.InterimIconWorldHeight;
    private readonly List<(Texture2D Texture, Color Tint)> _icons = new();
    private string? _text;
    private readonly List<Node> _built = new();
    private WorldUiLabel? _builtText;
    private bool _fallbackLogged;
    private bool _labelFallbackLogged;

    /// <summary>
    /// The scene the row instances for its text: a root that is a <see cref="WorldUiLabel"/>. Null, or a scene
    /// with another root, draws no text: the row warns in the dock and logs one error at runtime.
    /// </summary>
    [Export]
    public PackedScene? LabelScene { get; set; }

    /// <summary>
    /// Height of every icon in the row, in metres; text is not affected. Callers write this property
    /// rather than passing a height per call. The default is the house icon height,
    /// <see cref="WorldUiStyle.InterimIconWorldHeight"/>; author a value only to depart from it.
    ///
    /// <para>
    /// <c>0</c> is rejected: it means authored as 0, or null-stripped by a Godot editor resave (a
    /// value-type export equal to its C# default is written as null and reloads as 0). Both raise a
    /// configuration warning and one runtime warning, and the row draws the house icon height.
    /// </para>
    /// </summary>
    [Export(PropertyHint.Range, "0,2,0.01,suffix:m")]
    public float IconWorldHeight
    {
        get => _iconWorldHeight;
        set
        {
            _iconWorldHeight = value;
            UpdateConfigurationWarnings();
            Rebuild();
        }
    }

    /// <summary>
    /// The height the row draws, in metres: the icon height (fallback included) for icons, and the
    /// built label's drawn line, which is taller than its em, for text.
    /// </summary>
    public float RenderedHeight => _builtText != null && IsInstanceValid(_builtText) ? _builtText.RenderedHeight : IconHeight;

    private float IconHeight => WorldUiScale.IsUsable(_iconWorldHeight) ? _iconWorldHeight : WorldUiStyle.InterimIconWorldHeight;

    /// <inheritdoc/>
    public event Action LayoutChanged = delegate { };

    /// <summary>
    /// Replaces the row's items with one icon per entry with a texture, in order, each tinted by its own
    /// colour. An entry with a null texture is skipped; all null leaves the row empty.
    /// </summary>
    public void SetIcons(IReadOnlyList<(Texture2D? Texture, Color Tint)> icons)
    {
        _icons.Clear();
        _text = null;
        foreach ((Texture2D? texture, Color tint) in icons)
        {
            if (texture != null)
            {
                _icons.Add((texture, tint));
            }
        }

        Rebuild();
    }

    /// <summary>Replaces the row's items with one untinted (white) icon per non-null texture, in order.</summary>
    public void SetIcons(IReadOnlyList<Texture2D?> textures)
        => SetIcons(textures.Select(texture => (texture, Colors.White)).ToList());

    /// <summary>
    /// Replaces the row's items with one line of <paramref name="text"/> in the house world-UI font, at the
    /// house world-text height.
    /// </summary>
    public void SetText(string text)
    {
        _icons.Clear();
        _text = text;
        Rebuild();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (!WorldUiScale.IsUsable(_iconWorldHeight))
        {
            warnings.Add(IconWorldHeightFault());
        }

        warnings.AddRange(GetChildren().Where(child => !_built.Contains(child)).Select(child =>
            $"'{child.Name}' is a child the row did not build. The row builds its own items from SetIcons/SetText and " +
            "neither sizes nor places this one; remove it."));
        return warnings.ToArray();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationChildOrderChanged)
        {
            UpdateConfigurationWarnings();
        }
    }

    private string IconWorldHeightFault()
        => WorldUiScale.UnusableLengthExplanation("Icon World Height", _iconWorldHeight, WorldUiStyle.InterimIconWorldHeight);

    private void Rebuild()
    {
        foreach (Node node in _built.Where(IsInstanceValid))
        {
            if (node.GetParent() == this) { RemoveChild(node); }
            node.QueueFree();
        }

        _built.Clear();
        _builtText = null;

        if (_text != null)
        {
            BuildText(_text);
        }
        else if (_icons.Count > 0)
        {
            BuildIcons(IconHeightLoggingFallback());
        }

        LayoutChanged();
    }

    private float IconHeightLoggingFallback()
    {
        if (!WorldUiScale.IsUsable(_iconWorldHeight) && !_fallbackLogged)
        {
            _fallbackLogged = true;
            JmoLogger.Warning(this, $"[WorldUi] {Name}: {IconWorldHeightFault()}");
        }

        return IconHeight;
    }

    private void BuildText(string text)
    {
        Node? instance = LabelScene?.Instantiate();
        if (instance is not WorldUiLabel label)
        {
            instance?.QueueFree();
            if (!_labelFallbackLogged)
            {
                _labelFallbackLogged = true;
                JmoLogger.Error(this, $"[WorldUi] {Name}: Label Scene is not a WorldUiLabel scene, so the text is not drawn.");
            }

            return;
        }

        label.Text = text;
        AddBuilt(label);
        _builtText = label;
    }

    private void AddBuilt(Node node)
    {
        _built.Add(node);
        AddChild(node);
    }

    private void BuildIcons(float height)
    {
        var widths = new float[_icons.Count];
        var pixelSizes = new float[_icons.Count];
        for (int i = 0; i < _icons.Count; i++)
        {
            // From each texture's OWN height: a shared pixel_size transfers pixel density, not size.
            pixelSizes[i] = WorldUiScale.PixelSizeForTexture(height, _icons[i].Texture.GetHeight());
            widths[i] = _icons[i].Texture.GetWidth() * pixelSizes[i];
        }

        float[] centres = WorldUiLayout.RowCentres(widths, 0f);
        for (int i = 0; i < _icons.Count; i++)
        {
            AddBuilt(new Sprite3D
            {
                Texture = _icons[i].Texture,
                Modulate = _icons[i].Tint,
                PixelSize = pixelSizes[i],
                Position = new Vector3(centres[i], 0f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = WorldUiLabel.WorldUiRenderPriority,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            });
        }
    }
}
