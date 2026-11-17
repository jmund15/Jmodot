namespace Jmodot.Implementation.Visual.WorldUi;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// A horizontal row of world-UI items (<see cref="IWorldUiElement"/>): icons from
/// <see cref="SetIcons(IReadOnlyList{ValueTuple{Texture2D, Color}})"/>, or one line of text from
/// <see cref="SetText"/>. Icons sit edge to edge, each <see cref="WorldUiStyle.IconWorldHeight"/> tall and as wide as
/// its own aspect, the row centred on its origin. Text draws a <see cref="WorldUiLabel"/> instanced from the style's
/// <see cref="WorldUiStyle.LabelScene"/>, at the style's text height, never at the icon height.
/// </summary>
/// <remarks>
/// The row owns every child it draws and rebuilds them on each call, so no caller can change an item
/// behind its back. It frees only the children it built: an authored child survives a rebuild and is
/// named in a configuration warning, since the row neither sizes nor places it. Built icons take their depth test
/// and render priority from the applied style. <c>[Tool]</c> so its configuration warnings reach the scene dock.
/// </remarks>
[GlobalClass, Tool]
public partial class WorldUiRow : Node3D, IWorldUiElement
{
    private readonly List<(Texture2D Texture, Color Tint)> _icons = new();
    private string? _text;
    private WorldUiStyle? _style;
    private readonly List<Node> _built = new();
    private WorldUiLabel? _builtText;
    private bool _labelFallbackLogged;
    private bool _labelSceneRejected;
    private float _presentationAlpha = 1f;

    private WorldUiStyle Style => _style ?? WorldUiStyle.Fallback;

    /// <summary>
    /// The height the row draws, in metres: the icon height for icons, and the built label's drawn line, which is
    /// taller than its em, for text.
    /// </summary>
    public float RenderedHeight => _builtText != null && IsInstanceValid(_builtText) ? _builtText.RenderedHeight : Style.IconWorldHeight;

    /// <inheritdoc/>
    public event Action LayoutChanged = delegate { };

    /// <summary>
    /// Applies the style's icon height, depth test and render priority, and its label scene for text, then rebuilds
    /// the row's items.
    /// </summary>
    /// <summary>
    /// Multiplies <paramref name="alpha"/> into every built icon's own tint alpha and writes it to the built text. It is
    /// kept, so items the row rebuilds draw with it.
    /// </summary>
    public void SetPresentationAlpha(float alpha)
    {
        _presentationAlpha = alpha;
        ApplyPresentationAlpha();
    }

    public void ApplyStyle(WorldUiStyle style)
    {
        _style = style;
        Rebuild();
    }

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

    /// <summary>Replaces the row's items with one line of <paramref name="text"/> at the style's text height.</summary>
    public void SetText(string text)
    {
        _icons.Clear();
        _text = text;
        Rebuild();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (_style != null)
        {
            warnings.AddRange(_style.Faults());
        }

        if (_text != null && Style.LabelScene == null)
        {
            warnings.Add("The row has text but its style has no Label Scene, so the text is not drawn.");
        }
        else if (_labelSceneRejected)
        {
            warnings.Add("The style's Label Scene does not have a WorldUiLabel root, so the row's text is not drawn.");
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

    private void Rebuild()
    {
        foreach (Node node in _built.Where(IsInstanceValid))
        {
            if (node.GetParent() == this) { RemoveChild(node); }
            node.QueueFree();
        }

        _built.Clear();
        _builtText = null;
        _labelSceneRejected = false;

        if (_text != null)
        {
            BuildText(_text);
        }
        else if (_icons.Count > 0)
        {
            BuildIcons(Style.IconWorldHeight);
        }

        ApplyPresentationAlpha();
        UpdateConfigurationWarnings();
        LayoutChanged();
    }

    private void ApplyPresentationAlpha()
    {
        _builtText?.SetPresentationAlpha(_presentationAlpha);
        int icon = 0;
        foreach (Sprite3D sprite in _built.OfType<Sprite3D>())
        {
            Color tint = _icons[icon++].Tint;
            sprite.Modulate = new Color(tint, tint.A * _presentationAlpha);
        }
    }

    private void BuildText(string text)
    {
        Node? instance = Style.LabelScene?.Instantiate();
        _labelSceneRejected = instance != null && instance is not WorldUiLabel;
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
        label.ApplyStyle(Style);
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
        bool onTop = Style.DepthMode == WorldUiDepthMode.AlwaysOnTop;
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
                NoDepthTest = onTop,
                RenderPriority = onTop ? WorldUiStyle.OnTopRenderPriority : 0,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            });
        }
    }
}
