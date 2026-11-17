namespace Jmodot.Implementation.Visual.WorldUi;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Visual.Effects;
using Jmodot.Implementation.Components;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Visual;
using GCol = Godot.Collections;

/// <summary>
/// Places world-space UI around its host's MEASURED art: the <see cref="Above"/> elements stack
/// upward from the art's top, the <see cref="Below"/> elements downward from its bottom, each
/// separated by the style's <see cref="WorldUiStyle.AnchorPadding"/> in screen space. The host is the parent; the elements are
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
public partial class WorldUiPrompt : Node3D, IVisualEffectExempt
{
    private GCol.Array<Node3D> _above = new();
    private GCol.Array<Node3D> _below = new();
    private readonly List<IWorldUiElement> _subscribed = new();
    private EntitySizeController? _hostSize;

    private VisualBounds3D _bounds = VisualBounds3D.Unmeasured;
    private bool _measurePending = true;
    private bool _layoutPending = true;
    private float _appliedScreenUp = float.NaN;
    private bool _styleFaultsLogged;
    private bool _unmeasuredLogged;
    private bool _placementLogged;

    private WorldUiStyle? _style;

    /// <summary>
    /// The look this prompt draws with. Null is a configuration warning, and the prompt draws with
    /// <see cref="WorldUiStyle.Fallback"/>. Setting it applies the style to every element child.
    /// </summary>
    [Export]
    public WorldUiStyle? Style
    {
        get => _style;
        set
        {
            _style = value;
            ApplyStyleToElements();
            _layoutPending = true;
            UpdateConfigurationWarnings();
        }
    }

    private readonly Dictionary<StringName, bool> _gates = new();
    private bool _shown = true;
    private bool _motionSettled;
    private Tween? _tween;
    private float _presentationAlpha = 1f;
    private Vector3 _presentationOffset = Vector3.Zero;
    private float _presentationScale = 1f;

    /// <summary>True while every gate set through <see cref="SetGate"/> is open; true with no gates.</summary>
    public bool IsShown => _shown;

    /// <summary>Raised with the new <see cref="IsShown"/> when a <see cref="SetGate"/> changes it, after the show or hide has begun.</summary>
    public event Action<bool> ShownChanged = delegate { };

    /// <summary>Alpha of every element, 0 to 1. Driven by a transition; a caller reads it and never needs to write it.</summary>
    public float PresentationAlpha
    {
        get => _presentationAlpha;
        set
        {
            _presentationAlpha = value;
            _layoutPending = true;
            ApplyPresentationAlpha();
        }
    }

    /// <summary>Offset of every element from its laid-out position, in metres. Driven by a transition.</summary>
    public Vector3 PresentationOffset
    {
        get => _presentationOffset;
        set
        {
            _presentationOffset = value;
            _layoutPending = true;
        }
    }

    /// <summary>Uniform scale of every element. Driven by a transition.</summary>
    public float PresentationScale
    {
        get => _presentationScale;
        set
        {
            _presentationScale = value;
            _layoutPending = true;
        }
    }

    /// <summary>
    /// Opens or closes the named gate. The prompt is shown while every gate that has been set is open, so any
    /// number of triggers share it, each owning its own name.
    /// </summary>
    /// <remarks>
    /// A call that leaves <see cref="IsShown"/> unchanged raises nothing and starts no motion. A change runs the
    /// style's <see cref="WorldUiStyle.Transition"/> from the current presentation channels, reversing one that is
    /// still running. It applies at once, with no motion, when there is no transition or it has no usable
    /// duration, and for a change made before the prompt has processed a visible frame, so a gate that starts
    /// closed never flashes the prompt. The motion plays as soon as the prompt has been hidden by such a change.
    /// </remarks>
    public void SetGate(StringName gate, bool open)
    {
        _gates[gate] = open;
        bool shown = _gates.Values.All(isOpen => isOpen);
        if (shown == _shown) { return; }

        _shown = shown;
        PlayShownChange(shown);
        ShownChanged(shown);
    }

    private void PlayShownChange(bool shown)
    {
        _tween?.Kill();
        _tween = null;

        WorldUiTransition? transition = _style?.Transition;
        if (transition == null || !IsInsideTree())
        {
            ApplyRestState();
            Visible = shown;
            _motionSettled |= !shown;
            return;
        }

        bool animate = _motionSettled && transition.EffectiveDuration > 0f;
        _tween = CreateTween().SetParallel(true);
        if (shown)
        {
            Visible = true;
            transition.AddShowTweeners(_tween, this);
        }
        else
        {
            transition.AddHideTweeners(_tween, this);
        }

        _tween.Chain().TweenCallback(Callable.From(shown ? OnShowFinished : OnHideFinished));
        if (animate)
        {
#if TOOLS
            AnimatedTweensStartedForTest++;
#endif
            return;
        }

        _tween.CustomStep(SnapStepSeconds);
        _motionSettled |= !shown;
    }

    /// <summary>A step longer than any transition: finishes every tweener and the closing callback at once.</summary>
    private const double SnapStepSeconds = 1e6;

    private void OnShowFinished() => _tween = null;

    private void OnHideFinished()
    {
        _tween = null;
        Visible = false;
    }

    private void ApplyRestState()
    {
        PresentationAlpha = 1f;
        PresentationOffset = Vector3.Zero;
        PresentationScale = 1f;
    }

    private void ApplyPresentationAlpha()
    {
        foreach (IWorldUiElement element in _subscribed)
        {
            element.SetPresentationAlpha(_presentationAlpha);
        }
    }

    private WorldUiStyle ResolvedStyle => _style ?? WorldUiStyle.Fallback;

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
            ? EntityVisualBounds3D.Measure(host, exclude: this, extent: ArtExtent.DrawnPixels)
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
        // The Style setter runs while the scene loads, before the element children exist, so the
        // editor preview and the game both take the style here.
        ApplyStyleToElements();
        if (Engine.IsEditorHint()) { return; }

        FollowVisibility();
        LogStyleFaultsOnce();
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

        _motionSettled = true;
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

        List<IWorldUiElement> joined = placeable.Where(element => !_subscribed.Contains(element)).ToList();
        Unsubscribe();
        foreach (IWorldUiElement element in placeable)
        {
            element.LayoutChanged += OnElementLayoutChanged;
            _subscribed.Add(element);
        }

        if (IsNodeReady())
        {
            foreach (IWorldUiElement element in joined)
            {
                element.ApplyStyle(ResolvedStyle);
            }
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

    private void ApplyStyleToElements()
    {
        WorldUiStyle style = ResolvedStyle;
        foreach (IWorldUiElement element in GetChildren().OfType<IWorldUiElement>())
        {
            element.ApplyStyle(style);
        }
    }

    private void LogStyleFaultsOnce()
    {
        if (_styleFaultsLogged || Engine.IsEditorHint()) { return; }

        List<string> faults = StyleFaults().ToList();
        if (faults.Count == 0) { return; }

        _styleFaultsLogged = true;
        JmoLogger.Warning(this, $"[WorldUi] {Name}: {string.Join(" ", faults)}");
    }

    private IEnumerable<string> StyleFaults()
    {
        if (_style == null)
        {
            yield return "No Style is set, so the prompt draws with the framework fallback style.";
            yield break;
        }

        foreach (string fault in _style.Faults())
        {
            yield return fault;
        }

        if (_style.Transition != null)
        {
            foreach (string fault in _style.Transition.Faults())
            {
                yield return fault;
            }
        }
    }

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

        float centreY = measured ? _bounds.Center.Y + (_bounds.TrimShift.Y * ((1f / screenUp) - 1f)) : 0f;
        float halfHeight = measured ? _bounds.Size.Y * 0.5f : 0f;
        float centreX = measured ? _bounds.Center.X : 0f;
        float padding = ResolvedStyle.AnchorPadding;

        Transform = new Transform3D(Basis.Identity, new Vector3(0f, 0f, _bounds.Center.Z));
        Place(PlaceableAbove(), centreX, centreY, halfHeight, padding, screenUp, 1);
        Place(PlaceableBelow(), centreX, centreY, halfHeight, padding, screenUp, -1);
        if (ResolvedStyle.DepthMode == WorldUiDepthMode.SortedWithWorld)
        {
            LiftAboveHostGround(PlaceableAbove().Concat(PlaceableBelow()), padding);
        }

        foreach ((Node3D node, IWorldUiElement element) in PlaceableAbove().Concat(PlaceableBelow()))
        {
            node.Position += _presentationOffset;
            node.Scale = Vector3.One * _presentationScale;
            element.SetPresentationAlpha(_presentationAlpha);
        }

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

    /// <summary>
    /// Keeps a depth-tested element out of the ground it would otherwise sink into. The stacks grow along
    /// world Y at the art's depth, so under a pitched camera a below-art element lands under the host's
    /// ground and the ground would hide it. An element whose drawn bottom edge falls under the host origin
    /// plus <paramref name="padding"/> moves along the camera's view axis toward the camera until it clears;
    /// an orthographic camera draws it at the same screen position. A perspective camera is left unlifted,
    /// since moving toward it would change the element's drawn size.
    /// </summary>
    private void LiftAboveHostGround(IEnumerable<(Node3D Node, IWorldUiElement Element)> elements, float padding)
    {
        Camera3D? camera = GetViewport()?.GetCamera3D();
        if (camera == null || camera.Projection != Camera3D.ProjectionType.Orthogonal) { return; }

        Basis toLocal = GlobalBasis.Inverse();
        Vector3 back = (toLocal * camera.GlobalBasis.Z).Normalized();
        Vector3 up = (toLocal * camera.GlobalBasis.Y).Normalized();
        if (!(back.Y > 0.01f)) { return; }

        foreach ((Node3D node, IWorldUiElement element) in elements)
        {
            float requiredCentreY = padding + (Mathf.Max(up.Y, 0f) * element.RenderedHeight * 0.5f);
            float shortfall = requiredCentreY - node.Position.Y;
            if (!(shortfall > 0f)) { continue; }

            node.Position += back * (shortfall / back.Y);
        }
    }

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

        if (includeWarningOnlyFaults)
        {
            faults.AddRange(StyleFaults());
        }

        return faults;
    }

    #region Test Helpers
#if TOOLS
    internal int LayoutCountForTest { get; private set; }

    /// <summary>Counts the show and hide tweens that actually animate, so a test can tell a snap from a tween.</summary>
    internal int AnimatedTweensStartedForTest { get; private set; }
#endif
    #endregion
}
