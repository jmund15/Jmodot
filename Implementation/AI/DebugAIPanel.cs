namespace Jmodot.Implementation.AI;

using System.Collections.Generic;
using System.Linq;
using Shared;

/// <summary>
/// Abstract base class for AI debug overlay panels. Provides:
/// - CanvasLayer management for proper rendering in 3D scenes
/// - Corner slot registry for multi-instance stacking
/// - Follow-owner mode for tracking a Node3D on screen
/// - Managed tween lifecycle to prevent stacking/leaks
/// </summary>
public abstract partial class DebugAIPanel : Control
{
    public enum DebugViewPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        FollowOwner
    }

    private const float SLOT_MARGIN = 10f;

    #region Slot Registry (static, shared across all panels)

    private static readonly Dictionary<DebugViewPosition, List<DebugAIPanel>> _slotRegistry = new();

    /// <summary>
    /// Returns the current slot index for this panel in its corner, or -1 if not registered.
    /// </summary>
    public int SlotIndex { get; private set; } = -1;

    /// <summary>
    /// Exposes slot registry for testing. Returns the number of panels at a given position.
    /// </summary>
    public static int GetSlotCount(DebugViewPosition position)
    {
        if (!_slotRegistry.TryGetValue(position, out var list)) { return 0; }
        return list.Count;
    }

    /// <summary>
    /// Clears the entire slot registry. For testing only.
    /// </summary>
    public static void ClearSlotRegistry()
    {
        _slotRegistry.Clear();
    }

    private void RegisterSlot()
    {
        if (DisplayPosition == DebugViewPosition.FollowOwner) { return; }
        if (!_slotRegistry.ContainsKey(DisplayPosition))
        {
            _slotRegistry[DisplayPosition] = new List<DebugAIPanel>();
        }

        var list = _slotRegistry[DisplayPosition];
        if (!list.Contains(this))
        {
            list.Add(this);
        }
        SlotIndex = list.IndexOf(this);
    }

    private void UnregisterSlot()
    {
        if (DisplayPosition == DebugViewPosition.FollowOwner) { return; }
        if (!_slotRegistry.TryGetValue(DisplayPosition, out var list)) { return; }

        list.Remove(this);
        SlotIndex = -1;

        // Compact remaining panels
        for (int i = 0; i < list.Count; i++)
        {
            list[i].SlotIndex = i;
            list[i].ApplySlotOffset();
        }
    }

    #endregion

    #region Properties

    public DebugViewPosition DisplayPosition { get; private set; } = DebugViewPosition.TopLeft;
    public Node3D? OwnerNode { get; private set; }

    /// <summary>
    /// The size of this debug panel. Subclasses should set this before calling ShowPanel().
    /// </summary>
    protected Vector2 PanelSize { get; set; } = new Vector2(300, 200);

    private CanvasLayer? _canvasLayer;
    private readonly Dictionary<ulong, Tween> _managedTweens = new();

    #endregion

    #region Lifecycle

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }

        _canvasLayer = new CanvasLayer { Name = $"{Name}Canvas" };
        AddChild(_canvasLayer);

        // Reparent this control under the canvas layer for proper screen-space rendering
        // We can't reparent ourselves, so we'll add content to the canvas layer instead
        // Subclasses should add their content via GetContentParent()

        Hide();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) { return; }

        if (DisplayPosition == DebugViewPosition.FollowOwner && OwnerNode.IsValid())
        {
            UpdateFollowPosition();
        }
    }

    public override void _ExitTree()
    {
        Cleanup();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Sets the display position for this panel and applies the appropriate anchors/offsets.
    /// </summary>
    public void SetDisplayPosition(DebugViewPosition position)
    {
        DisplayPosition = position;
        ApplyPositionAnchors();
    }

    /// <summary>
    /// Sets the Node3D to follow when DisplayPosition is FollowOwner.
    /// </summary>
    public void SetOwnerNode(Node3D? owner)
    {
        OwnerNode = owner;
    }

    /// <summary>
    /// Shows the panel and registers it in the slot registry.
    /// </summary>
    public void ShowPanel()
    {
        RegisterSlot();
        ApplySlotOffset();
        Show();
    }

    /// <summary>
    /// Hides the panel and unregisters it from the slot registry.
    /// </summary>
    public void HidePanel()
    {
        UnregisterSlot();
        Hide();
    }

    #endregion

    #region Positioning

    private void ApplyPositionAnchors()
    {
        Size = PanelSize;

        switch (DisplayPosition)
        {
            case DebugViewPosition.TopLeft:
                SetAnchorsPreset(LayoutPreset.TopLeft);
                break;
            case DebugViewPosition.TopRight:
                SetAnchorsPreset(LayoutPreset.TopRight);
                break;
            case DebugViewPosition.BottomLeft:
                SetAnchorsPreset(LayoutPreset.BottomLeft);
                break;
            case DebugViewPosition.BottomRight:
                SetAnchorsPreset(LayoutPreset.BottomRight);
                break;
            case DebugViewPosition.FollowOwner:
                // Position is updated each frame in _Process
                break;
        }
    }

    private void ApplySlotOffset()
    {
        if (SlotIndex < 0) { return; }

        float yOffset = SlotIndex * (PanelSize.Y + SLOT_MARGIN);
        float margin = SLOT_MARGIN;

        switch (DisplayPosition)
        {
            case DebugViewPosition.TopLeft:
                Position = new Vector2(margin, margin + yOffset);
                break;
            case DebugViewPosition.TopRight:
                Position = new Vector2(-margin - PanelSize.X, margin + yOffset);
                break;
            case DebugViewPosition.BottomLeft:
                Position = new Vector2(margin, -margin - PanelSize.Y - yOffset);
                break;
            case DebugViewPosition.BottomRight:
                Position = new Vector2(-margin - PanelSize.X, -margin - PanelSize.Y - yOffset);
                break;
        }
    }

    private void UpdateFollowPosition()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera == null || !OwnerNode.IsValid()) { return; }

        var screenPos = camera.UnprojectPosition(OwnerNode!.GlobalPosition);
        var viewportSize = GetViewportRect().Size;

        // Clamp to viewport bounds
        float x = Mathf.Clamp(screenPos.X, 0, viewportSize.X - PanelSize.X);
        float y = Mathf.Clamp(screenPos.Y, 0, viewportSize.Y - PanelSize.Y);

        Position = new Vector2(x, y);
    }

    #endregion

    #region Tween Management

    /// <summary>
    /// Creates a tween that is tracked by target node instance ID.
    /// If a tween already exists for this target, it is killed first (prevents stacking).
    /// </summary>
    protected Tween CreateManagedTween(Node target)
    {
        ulong id = target.GetInstanceId();
        if (_managedTweens.TryGetValue(id, out var existing) && existing.IsValid())
        {
            existing.Kill();
        }

        var tween = CreateTween();
        _managedTweens[id] = tween;
        return tween;
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Cleans up all resources. Called from _ExitTree(). Override in subclasses for additional cleanup.
    /// </summary>
    protected virtual void Cleanup()
    {
        UnregisterSlot();

        foreach (var tween in _managedTweens.Values)
        {
            if (tween.IsValid())
            {
                tween.Kill();
            }
        }
        _managedTweens.Clear();
    }

    #endregion
}
