namespace Jmodot.Implementation.AI.Debug;

using System.Collections.Generic;
using System.Linq;
using Jmodot.Core.AI;
using Shared;

/// <summary>
///     Unified AI debug dashboard that auto-discovers all <see cref="IDebugPanelProvider"/>
///     subsystems on an entity and presents them in a tabbed interface. Supports:
///     <list type="bullet">
///         <item>Automatic tree-walk discovery of providers</item>
///         <item>Tab bar for multiple providers (hidden when single provider)</item>
///         <item>Lazy content creation — only when tab is first selected</item>
///         <item>Active-only updates — only the visible tab gets per-frame calls</item>
///         <item>HSM→BT drill-down via <see cref="IDebugNestingProvider"/></item>
///         <item>Draggable + resizable panel with click-to-focus z-ordering</item>
///     </list>
///
///     <para>Add this node to an entity's scene tree. Call <see cref="Initialize"/> after
///     all AI subsystems are initialized.</para>
/// </summary>
public partial class AIDebugDashboard : Node
{
    #region Exports

    [Export] public DebugAIPanel.DebugViewPosition InitialAnchor { get; set; } = DebugAIPanel.DebugViewPosition.TopRight;
    [Export] public Vector2 InitialSize { get; set; } = new(400, 500);

    #endregion

    #region State

    private readonly List<IDebugPanelProvider> _rootProviders = new();
    private readonly Dictionary<IDebugPanelProvider, List<IDebugPanelProvider>> _nestedMap = new();
    private readonly Dictionary<IDebugPanelProvider, Control> _contentCache = new();
    private readonly HashSet<IDebugPanelProvider> _allNestedProviders = new();

    private CanvasLayer? _canvasLayer;
    private PanelContainer? _panelContainer;
    private VBoxContainer? _mainLayout;
    private HBoxContainer? _titleBar;
    private HBoxContainer? _tabBar;
    private MarginContainer? _contentArea;

    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragOffset;
    private Vector2 _resizeStart;
    private Vector2 _startSize;

    private static int _nextLayer = 100;
    private static readonly Vector2 MinPanelSize = new(300, 200);
    private static readonly Vector2 MaxPanelSize = new(800, 900);
    private static readonly Vector2 DefaultViewportSize = new(1920, 1080);
    private const float TITLE_BAR_HEIGHT = 32f;
    private const float RESIZE_GRIP_SIZE = 16f;

    private Control? _resizeGrip;

    #endregion

    #region Public API

    /// <summary>The currently active (visible) tab's provider, or null if no providers.</summary>
    public IDebugPanelProvider? ActiveProvider { get; private set; }

    /// <summary>Number of root-level providers (each gets its own tab).</summary>
    public int ProviderCount => _rootProviders.Count;

    /// <summary>Whether the tab bar is visible (hidden when only 1 provider).</summary>
    public bool IsTabBarVisible => _tabBar?.Visible ?? false;

    /// <summary>Whether the dashboard has been initialized.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     The currently expanded nested provider (e.g., the active BTState's BT).
    ///     Auto-updated when a nesting provider fires ActiveNestedProviderChanged.
    /// </summary>
    public IDebugPanelProvider? ExpandedNestedProvider { get; private set; }

    /// <summary>The current CanvasLayer.Layer value for z-ordering.</summary>
    public int CurrentLayer => _canvasLayer?.Layer ?? 0;

    /// <summary>The current panel position (for testing drag behavior).</summary>
    public Vector2 PanelPosition => _panelContainer?.Position ?? Vector2.Zero;

    /// <summary>The current panel size (for testing resize behavior).</summary>
    public Vector2 PanelSize => _panelContainer?.Size ?? Vector2.Zero;

    /// <summary>
    ///     Returns the root provider at the given index (ordered by DebugTabOrder).
    /// </summary>
    public IDebugPanelProvider GetProviderAt(int index) => _rootProviders[index];

    /// <summary>
    ///     Returns nested providers for a root provider, or empty list if none.
    /// </summary>
    public IReadOnlyList<IDebugPanelProvider> GetNestedProviders(IDebugPanelProvider rootProvider)
    {
        if (_nestedMap.TryGetValue(rootProvider, out var nested))
        {
            return nested;
        }
        return System.Array.Empty<IDebugPanelProvider>();
    }

    /// <summary>
    ///     Discovers all <see cref="IDebugPanelProvider"/> children of the entity root,
    ///     builds the visual hierarchy, and selects the first tab.
    ///     Call after all AI subsystems are initialized.
    /// </summary>
    public void Initialize(Node entityRoot)
    {
        if (IsInitialized) { return; }

        DiscoverProviders(entityRoot);
        BuildVisualHierarchy(entityRoot);

        if (_rootProviders.Count > 0)
        {
            SelectTab(_rootProviders[0]);
        }

        IsInitialized = true;
    }

    /// <summary>
    ///     Switches the active tab to the given provider. Lazy-creates content on
    ///     first selection. Notifies the old provider it is hidden.
    /// </summary>
    public void SelectTab(IDebugPanelProvider provider)
    {
        if (!_rootProviders.Contains(provider)) { return; }

        // Hide old tab
        if (ActiveProvider != null && ActiveProvider != provider)
        {
            ActiveProvider.OnDebugContentHidden();
            if (_contentCache.TryGetValue(ActiveProvider, out var oldContent))
            {
                oldContent.Visible = false;
            }
        }

        ActiveProvider = provider;

        // Lazy-create content
        if (!_contentCache.ContainsKey(provider))
        {
            var content = provider.CreateDebugContent();
            _contentCache[provider] = content;
            _contentArea?.AddChild(content);
        }

        // Show new tab content
        if (_contentCache.TryGetValue(provider, out var newContent))
        {
            newContent.Visible = true;
        }

        UpdateTabBarSelection();
    }

    /// <summary>
    ///     Updates the active tab's content. Called per frame from _Process.
    /// </summary>
    public void UpdateActiveTab(double delta)
    {
        ActiveProvider?.UpdateDebugContent(delta);
    }

    #endregion

    #region Discovery

    private void DiscoverProviders(Node entityRoot)
    {
        _rootProviders.Clear();
        _nestedMap.Clear();
        _allNestedProviders.Clear();

        // Discover all providers in the entity tree
        var allProviders = entityRoot.GetChildrenOfInterface<IDebugPanelProvider>();

        // First pass: collect all nested providers from nesting providers
        foreach (var provider in allProviders)
        {
            if (provider is IDebugNestingProvider nesting)
            {
                var nested = nesting.NestedProviders;
                if (nested.Count > 0)
                {
                    _nestedMap[provider] = new List<IDebugPanelProvider>(nested);
                    foreach (var n in nested)
                    {
                        _allNestedProviders.Add(n);
                    }
                }
            }
        }

        // Second pass: root providers are those NOT in any nesting provider's list
        // and NOT the dashboard itself
        foreach (var provider in allProviders)
        {
            if (provider is AIDebugDashboard) { continue; }
            if (_allNestedProviders.Contains(provider)) { continue; }
            _rootProviders.Add(provider);
        }

        // Sort by DebugTabOrder
        _rootProviders.Sort((a, b) => a.DebugTabOrder.CompareTo(b.DebugTabOrder));

        // Subscribe to nesting provider events
        foreach (var provider in _rootProviders)
        {
            if (provider is IDebugNestingProvider nesting)
            {
                nesting.ActiveNestedProviderChanged += OnActiveNestedChanged;
            }
        }
    }

    private void OnActiveNestedChanged(IDebugPanelProvider? newActive)
    {
        ExpandedNestedProvider = newActive;
    }

    #endregion

    #region Visual Hierarchy

    private void BuildVisualHierarchy(Node entityRoot)
    {
        // CanvasLayer for overlay rendering
        _canvasLayer = new CanvasLayer
        {
            Name = "DashboardCanvas",
            Layer = _nextLayer++
        };
        AddChild(_canvasLayer);

        // Main panel container
        _panelContainer = new PanelContainer
        {
            Name = "DashboardPanel",
            CustomMinimumSize = new Vector2(300, 200),
            Size = InitialSize
        };
        _canvasLayer.AddChild(_panelContainer);

        // Main vertical layout
        _mainLayout = new VBoxContainer { Name = "MainLayout" };
        _panelContainer.AddChild(_mainLayout);

        // Title bar
        _titleBar = new HBoxContainer { Name = "TitleBar" };
        var titleLabel = new Label
        {
            Text = entityRoot.Name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleBar.AddChild(titleLabel);

        var closeButton = new Button { Text = "X", CustomMinimumSize = new Vector2(24, 24) };
        closeButton.Pressed += OnClosePressed;
        _titleBar.AddChild(closeButton);
        _mainLayout.AddChild(_titleBar);

        // Tab bar (hidden when single provider)
        _tabBar = new HBoxContainer { Name = "TabBar" };
        foreach (var provider in _rootProviders)
        {
            var tabButton = new Button
            {
                Text = provider.DebugTabName,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(60, 28)
            };
            var capturedProvider = provider;
            tabButton.Pressed += () => SelectTab(capturedProvider);
            _tabBar.AddChild(tabButton);
        }
        _tabBar.Visible = _rootProviders.Count > 1;
        _mainLayout.AddChild(_tabBar);

        // Content area
        _contentArea = new MarginContainer { Name = "ContentArea" };
        _contentArea.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _mainLayout.AddChild(_contentArea);

        // Resize grip (bottom-right corner)
        _resizeGrip = new Control
        {
            Name = "ResizeGrip",
            CustomMinimumSize = new Vector2(RESIZE_GRIP_SIZE, RESIZE_GRIP_SIZE),
            MouseDefaultCursorShape = Control.CursorShape.Fdiagsize
        };
        _resizeGrip.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _resizeGrip.GrowHorizontal = Control.GrowDirection.Begin;
        _resizeGrip.GrowVertical = Control.GrowDirection.Begin;
        _resizeGrip.GuiInput += OnResizeGripGuiInput;
        _panelContainer.AddChild(_resizeGrip);

        // Wire input handlers
        _panelContainer.GuiInput += OnPanelGuiInput;

        // Apply initial position
        ApplyInitialPosition();
    }

    private void ApplyInitialPosition()
    {
        if (_panelContainer == null) { return; }

        float margin = 10f;
        var size = InitialSize;

        // We can't reliably get viewport size in editor-hint mode,
        // so use reasonable defaults for anchored positioning
        var viewportSize = DefaultViewportSize;
        if (IsInsideTree())
        {
            var viewport = GetViewport();
            if (viewport != null)
            {
                viewportSize = viewport.GetVisibleRect().Size;
            }
        }

        switch (InitialAnchor)
        {
            case DebugAIPanel.DebugViewPosition.TopLeft:
                _panelContainer.Position = new Vector2(margin, margin);
                break;
            case DebugAIPanel.DebugViewPosition.TopRight:
                _panelContainer.Position = new Vector2(viewportSize.X - size.X - margin, margin);
                break;
            case DebugAIPanel.DebugViewPosition.BottomLeft:
                _panelContainer.Position = new Vector2(margin, viewportSize.Y - size.Y - margin);
                break;
            case DebugAIPanel.DebugViewPosition.BottomRight:
                _panelContainer.Position = new Vector2(viewportSize.X - size.X - margin, viewportSize.Y - size.Y - margin);
                break;
            default:
                JmoLogger.Warning(this, $"Unsupported dashboard anchor '{InitialAnchor}', defaulting to TopRight.");
                _panelContainer.Position = new Vector2(viewportSize.X - size.X - margin, margin);
                break;
        }
    }

    private void UpdateTabBarSelection()
    {
        if (_tabBar == null) { return; }

        for (int i = 0; i < _rootProviders.Count && i < _tabBar.GetChildCount(); i++)
        {
            if (_tabBar.GetChild(i) is Button btn)
            {
                btn.ButtonPressed = _rootProviders[i] == ActiveProvider;
            }
        }
    }

    #endregion

    #region Lifecycle

    public override void _Process(double delta)
    {
        if (!IsInitialized) { return; }
        UpdateActiveTab(delta);
    }

    public override void _ExitTree()
    {
        foreach (var provider in _rootProviders)
        {
            if (provider is IDebugNestingProvider nesting)
            {
                nesting.ActiveNestedProviderChanged -= OnActiveNestedChanged;
            }
        }
    }

    #endregion

    #region Drag & Resize

    /// <summary>
    ///     Brings this dashboard to the front of all other dashboards.
    ///     Called on click-to-focus.
    /// </summary>
    public void BringToFront()
    {
        if (_canvasLayer != null)
        {
            _canvasLayer.Layer = ++_nextLayer;
        }
    }

    /// <summary>
    ///     Resets the static layer counter. For testing only.
    /// </summary>
    public static void ResetLayerCounter()
    {
        _nextLayer = 100;
    }

    private void OnPanelGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleMouseMotion(mouseMotion);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex != MouseButton.Left) { return; }

        if (mouseButton.Pressed)
        {
            BringToFront();

            var localPos = mouseButton.Position;

            // Check if in title bar region
            if (localPos.Y <= TITLE_BAR_HEIGHT)
            {
                _isDragging = true;
                _dragOffset = localPos;
            }
        }
        else
        {
            _isDragging = false;
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (_isDragging && _panelContainer != null)
        {
            _panelContainer.Position += mouseMotion.Relative;
        }
    }

    private void OnResizeGripGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _isResizing = true;
                _resizeStart = mouseButton.GlobalPosition;
                _startSize = _panelContainer?.Size ?? InitialSize;
            }
            else
            {
                _isResizing = false;
            }
        }
        else if (@event is InputEventMouseMotion && _isResizing && _panelContainer != null)
        {
            var mouseMotion = (InputEventMouseMotion)@event;
            var delta = mouseMotion.GlobalPosition - _resizeStart;
            var newSize = _startSize + delta;
            _panelContainer.Size = newSize.Clamp(MinPanelSize, MaxPanelSize);
        }
    }

    #endregion

    #region Event Handlers

    private void OnClosePressed()
    {
        if (_canvasLayer != null)
        {
            _canvasLayer.Visible = false;
        }
    }

    #endregion
}
