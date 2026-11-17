namespace Jmodot.Implementation.Visual.WorldUi;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Implementation.Components;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Visual;
using GCol = Godot.Collections;

/// <summary>
/// Places world-space UI around its host's MEASURED art: the <see cref="Above"/> elements stack
/// upward from the art's top, the <see cref="Below"/> elements downward from its bottom, each
/// separated by <see cref="AnchorPadding"/> in screen space. The host is the parent; the elements are
/// this node's direct children. Positions are automatic, so no element transform is authored.
/// </summary>
/// <remarks>
/// <para>
/// The prompt owns only position. Each <see cref="IWorldUiElement"/> owns how it renders and raises
/// <see cref="IWorldUiElement.LayoutChanged"/> when its size can change; the prompt then lays out
/// once, in its own <c>_Process</c>, however many changes arrived that frame. It also re-lays out when
/// the camera's pitch changes. Callers never order a "set, then refresh" pair.
/// </para>
/// <para>
/// The prompt follows a stack when it is assigned and when its own children change, so an element listed
/// before it is parented is picked up when it is parented, with its <see cref="IWorldUiElement.LayoutChanged"/>.
/// The getters return copies, so an in-place edit changes nothing: assign a new array.
/// </para>
/// <para>
/// A prompt that is not visible in the tree (hidden itself, or under a hidden host) does not process. It
/// lays out on its first visible frame, since whatever changed while it was hidden was never laid out.
/// </para>
/// <para>
/// Host art is measured on the first frame after the prompt enters the tree, so a prompt moved to a
/// new parent brackets the new host. It is measured again on the frame after the host's
/// <see cref="EntitySizeController"/>, when the host has one as a direct child, changes its applied scale, and
/// after <see cref="Remeasure"/>, which a caller whose host art changes by any other route must call. Layout
/// writes this node's whole transform: an identity basis at the host origin, at the art's measured depth,
/// overwriting any authored transform.
/// </para>
/// <para>
/// Misconfiguration (no <c>Node3D</c> parent, a listed node that is not a direct child, a node listed
/// in both stacks) raises a configuration warning and one <c>Error</c> at <c>_Ready</c>. A listed
/// node that is not an <see cref="IWorldUiElement"/> is skipped with a warning, and an element child
/// that neither stack lists is never placed and is named in a warning. <c>[Tool]</c> so the warnings
/// reach the scene dock; no layout or runtime log runs in the editor.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class WorldUiPrompt : Node3D
{
    private float _anchorPadding = WorldUiStyle.InterimAnchorPadding;
    private WorldUiStyle? _defaultStyle;
    private GCol.Array<Node3D> _above = new();
    private GCol.Array<Node3D> _below = new();
    private readonly List<IWorldUiElement> _subscribed = new();
    private EntitySizeController? _hostSize;

    private VisualBounds3D _bounds = VisualBounds3D.Unmeasured;
    private bool _measurePending = true;
    private bool _layoutPending = true;
    private float _appliedScreenUp = float.NaN;
    private bool _paddingFallbackLogged;
    private bool _unmeasuredLogged;
    private bool _placementLogged;

    /// <summary>
    /// Screen-space gap, in metres, between the art and the nearest element, and between stacked
    /// elements. The default is the house gap, <see cref="WorldUiStyle.InterimAnchorPadding"/>; author a
    /// value only to depart from it.
    ///
    /// <para>
    /// <c>0</c> is rejected, not "no padding": it means authored as 0, or null-stripped by a Godot editor
    /// resave (a value-type export equal to its C# default is written as null and reloads as 0). Both raise
    /// a configuration warning and one runtime warning, and the prompt uses the house gap; the smallest
    /// authorable gap is therefore 0.01 m.
    /// </para>
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01,suffix:m")]
    public float AnchorPadding
    {
        get => _anchorPadding;
        set
        {
            _anchorPadding = value;
            _layoutPending = true;
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>
    /// The look this prompt draws with. Null means a default style, created on first use. Its
    /// <c>AnchorPadding</c> is the gap used when <see cref="AnchorPadding"/> is unusable.
    /// </summary>
    [Export]
    public WorldUiStyle? Style { get; set; }

    private WorldUiStyle ResolvedStyle => Style ?? (_defaultStyle ??= new WorldUiStyle());

    /// <summary>Elements stacked above the art, nearest first. Each must be a direct child and a world-UI element (label or row).</summary>
    [Export]
    public GCol.Array<Node3D> Above
    {
        get => _above.Duplicate();
        set => ReplaceStack(ref _above, value);
    }

    /// <summary>Elements stacked below the art, nearest first. Each must be a direct child and a world-UI element (label or row).</summary>
    [Export]
    public GCol.Array<Node3D> Below
    {
        get => _below.Duplicate();
        set => ReplaceStack(ref _below, value);
    }

    /// <summary>
    /// Re-reads the host's art bounds and lays out on the next frame. Call it when the host's art changes by a
    /// route other than its <see cref="EntitySizeController"/>, which the prompt follows itself.
    /// </summary>
    public void Remeasure()
    {
        _measurePending = false;
        Node3D? host = ResolveHost();
        _bounds = host != null && host.IsInsideTree()
            ? EntityVisualBounds3D.Measure(host, exclude: this)
            : VisualBounds3D.Unmeasured;
        _layoutPending = true;
    }

    /// <summary>The node whose art is bracketed: the parent, when it is a <c>Node3D</c>.</summary>
    private Node3D? ResolveHost() => GetParent() as Node3D;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint()) { return; }

        _measurePending = true;
        _layoutPending = true;
        _placementLogged = false;
        ReconcileSubscriptions();
        FollowHostSize();
        FollowVisibility();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint()) { return; }

        Unsubscribe();
        UnfollowHostSize();
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }

        FollowVisibility();
        List<string> errors = Faults(includeWarningOnlyFaults: false);
        if (errors.Count > 0)
        {
            JmoLogger.Error(this, $"[WorldUi] {Name}: {string.Join(" ", errors)}");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationChildOrderChanged)
        {
            UpdateConfigurationWarnings();
            ReconcileSubscriptions();
        }
        else if (what == NotificationVisibilityChanged)
        {
            FollowVisibility();
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) { return; }

        if (_measurePending) { Remeasure(); }

        float screenUp = CurrentScreenUp();
        if (!_layoutPending && Mathf.IsEqualApprox(screenUp, _appliedScreenUp)) { return; }

        LayOut(screenUp);
    }

    public override string[] _GetConfigurationWarnings() => Faults(includeWarningOnlyFaults: true).ToArray();

    private void ReplaceStack(ref GCol.Array<Node3D> stack, GCol.Array<Node3D>? value)
    {
        stack = value ?? new GCol.Array<Node3D>();
        _layoutPending = true;
        UpdateConfigurationWarnings();
        ReconcileSubscriptions();
    }

    /// <summary>
    /// Processes only while visible in the tree; becoming visible lays out on the next frame, since a
    /// hidden prompt laid out nothing that changed meanwhile.
    /// </summary>
    private void FollowVisibility()
    {
        if (Engine.IsEditorHint() || !IsInsideTree()) { return; }

        bool visible = IsVisibleInTree();
        if (visible)
        {
            _layoutPending = true;
        }

        SetProcess(visible);
    }

    /// <summary>
    /// Subscribes to exactly the elements the prompt places now. Runs when a stack is assigned, when this
    /// node's children change and on entering the tree, which together cover every way the placeable set
    /// can change except an in-place array edit.
    /// </summary>
    private void ReconcileSubscriptions()
    {
        if (Engine.IsEditorHint() || !IsInsideTree()) { return; }

        List<IWorldUiElement> placeable = PlaceableAbove().Concat(PlaceableBelow()).Select(pair => pair.Element).ToList();
        if (placeable.Count == _subscribed.Count && placeable.Zip(_subscribed).All(pair => ReferenceEquals(pair.First, pair.Second)))
        {
            return;
        }

        Unsubscribe();
        foreach (IWorldUiElement element in placeable)
        {
            element.LayoutChanged += OnElementLayoutChanged;
            _subscribed.Add(element);
        }

        _layoutPending = true;
    }

    private void Unsubscribe()
    {
        foreach (IWorldUiElement element in _subscribed)
        {
            element.LayoutChanged -= OnElementLayoutChanged;
        }

        _subscribed.Clear();
    }

    private void OnElementLayoutChanged() => _layoutPending = true;

    private void FollowHostSize()
    {
        UnfollowHostSize();
        if (ResolveHost() is { } host && host.TryGetFirstChildOfType<EntitySizeController>(out EntitySizeController? size) && size != null)
        {
            _hostSize = size;
            _hostSize.AppliedScaleChanged += OnHostScaleChanged;
        }
    }

    private void UnfollowHostSize()
    {
        if (_hostSize != null && IsInstanceValid(_hostSize))
        {
            _hostSize.AppliedScaleChanged -= OnHostScaleChanged;
        }

        _hostSize = null;
    }

    private void OnHostScaleChanged(float _) => _measurePending = true;

    private IEnumerable<(Node3D Node, IWorldUiElement Element)> PlaceableAbove() => Placeable(_above, exclude: null);

    /// <summary>An entry listed in both stacks is placed in Above only.</summary>
    private IEnumerable<(Node3D Node, IWorldUiElement Element)> PlaceableBelow() => Placeable(_below, exclude: _above);

    /// <summary>
    /// The one placeable filter, shared by subscription and layout: valid world-UI elements that are direct
    /// children, minus any already in <paramref name="exclude"/>.
    /// </summary>
    private IEnumerable<(Node3D Node, IWorldUiElement Element)> Placeable(GCol.Array<Node3D> stack, GCol.Array<Node3D>? exclude)
    {
        foreach (Node3D node in stack)
        {
            if (node != null && IsInstanceValid(node) && node.GetParent() == this && node is IWorldUiElement element
                && (exclude == null || !exclude.Contains(node)))
            {
                yield return (node, element);
            }
        }
    }

    private float CurrentScreenUp()
    {
        Camera3D? camera = GetViewport()?.GetCamera3D();
        return camera == null ? 1f : WorldUiLayout.ScreenUpFactor(camera.GlobalBasis.Y.Y);
    }

    private void LayOut(float screenUp)
    {
        _layoutPending = false;
        _appliedScreenUp = screenUp;
#if TOOLS
        LayoutCountForTest++;
#endif

        bool measured = _bounds.IsMeasured;
        if (!measured && !_unmeasuredLogged)
        {
            _unmeasuredLogged = true;
            JmoLogger.Warning(this, $"[WorldUi] {Name}: the host has no measurable art, so the prompt brackets the host origin.");
        }

        float centreY = measured ? _bounds.Center.Y : 0f;
        float halfHeight = measured ? _bounds.Size.Y * 0.5f : 0f;
        float centreX = measured ? _bounds.Center.X : 0f;
        float padding = EffectivePadding();

        Transform = new Transform3D(Basis.Identity, new Vector3(0f, 0f, _bounds.Center.Z));
        Place(PlaceableAbove(), centreX, centreY, halfHeight, padding, screenUp, 1);
        Place(PlaceableBelow(), centreX, centreY, halfHeight, padding, screenUp, -1);

        if (!_placementLogged)
        {
            _placementLogged = true;
            JmoLogger.Info(this, $"[WorldUi] {Name}: placed {PlaceableAbove().Count()} above and {PlaceableBelow().Count()} below " +
                $"{GetParent()?.Name} art {_bounds.Size.Y:F2} m tall, padding {padding:F2} m, screen-up {screenUp:F3}.");
        }
    }

    private static void Place(
        IEnumerable<(Node3D Node, IWorldUiElement Element)> stack, float centreX, float centreY, float halfHeight, float padding,
        float screenUp, int direction)
    {
        List<(Node3D Node, IWorldUiElement Element)> elements = stack.ToList();
        float[] centres = WorldUiLayout.StackCentres(
            centreY, halfHeight, padding, screenUp, elements.Select(e => e.Element.RenderedHeight).ToArray(), direction);

        for (int i = 0; i < elements.Count; i++)
        {
            elements[i].Node.Position = new Vector3(centreX, centres[i], 0f);
        }
    }

    private float EffectivePadding()
    {
        if (WorldUiScale.IsUsable(_anchorPadding)) { return _anchorPadding; }

        if (!_paddingFallbackLogged)
        {
            _paddingFallbackLogged = true;
            JmoLogger.Warning(this, $"[WorldUi] {Name}: {AnchorPaddingFault()}");
        }

        return ResolvedStyle.AnchorPadding;
    }

    private string AnchorPaddingFault()
        => WorldUiScale.UnusableLengthExplanation("Anchor Padding", _anchorPadding, ResolvedStyle.AnchorPadding);

    /// <summary>
    /// The dock warning for a world-UI element child that neither stack lists. A subtype that derives its
    /// stacks itself overrides it to name what the designer can act on.
    /// </summary>
    protected virtual string UnplacedElementFault(Node element)
        => $"'{element.Name}' is a world-UI element that neither Above nor Below lists; it is never placed.";

    /// <summary>
    /// Configuration faults. Errors (no host, an entry that is not a direct child, an entry in both
    /// stacks) always; with <paramref name="includeWarningOnlyFaults"/>, also the skipped non-elements and
    /// the padding sentinel. Type tests here are editor-safe because every element type is [Tool].
    /// </summary>
    private List<string> Faults(bool includeWarningOnlyFaults)
    {
        var faults = new List<string>();
        if (ResolveHost() == null)
        {
            faults.Add("The parent is not a Node3D, so there is no host art to place around.");
        }

        foreach ((GCol.Array<Node3D> stack, string stackName) in new[] { (_above, "Above"), (_below, "Below") })
        {
            foreach (Node3D node in stack)
            {
                if (node == null || !IsInstanceValid(node))
                {
                    if (includeWarningOnlyFaults) { faults.Add($"{stackName} has an empty slot."); }
                    continue;
                }

                if (node.GetParent() != this)
                {
                    faults.Add($"{stackName} lists '{node.Name}', which is not a direct child of this prompt; it is not placed.");
                    continue;
                }

                if (includeWarningOnlyFaults && node is not IWorldUiElement)
                {
                    faults.Add($"{stackName} lists '{node.Name}', which is not a world-UI element (WorldUiLabel or WorldUiRow); it is skipped.");
                }
            }
        }

        foreach (Node3D node in _above.Where(n => n != null && IsInstanceValid(n) && _below.Contains(n)))
        {
            faults.Add($"'{node.Name}' is listed in both Above and Below; it is placed in Above only.");
        }

        if (includeWarningOnlyFaults)
        {
            foreach (Node child in GetChildren().Where(c => c is IWorldUiElement && c is Node3D n && !_above.Contains(n) && !_below.Contains(n)))
            {
                faults.Add(UnplacedElementFault(child));
            }
        }

        if (includeWarningOnlyFaults && !WorldUiScale.IsUsable(_anchorPadding))
        {
            faults.Add(AnchorPaddingFault());
        }

        return faults;
    }

    #region Test Helpers
#if TOOLS
    internal int LayoutCountForTest { get; private set; }
#endif
    #endregion
}
