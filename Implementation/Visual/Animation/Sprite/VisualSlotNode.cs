namespace Jmodot.Implementation.Visual.Animation.Sprite;

using System;
using System.Collections.Generic;
using Godot;
using GCol = Godot.Collections;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Core.Visual.Effects;
using Shared;

/// <summary>
/// A first-class scene-graph slot — replaces the legacy <c>VisualSlotConfig</c> +
/// <c>VisualSlot</c> pair. Each slot is a <see cref="Node3D"/> child of the composer
/// that owns its own mount point (its children become the equipped instance).
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IVisualNodeProvider"/> so the composer can aggregate slots
/// 1:1 by forwarding events from each child. Atomic event firing: <see cref="Equip"/>
/// tears down the prior instance fully (no events) THEN fires <see cref="NodeRemoved"/>
/// for the gone handles, instantiates the new prefab, builds new handles, and fires
/// <see cref="NodeAdded"/> for each. Subscribers calling <see cref="GetVisualNodes"/>
/// inside an event handler see consistent state.
/// </para>
/// <para>
/// <b>Push / Pop</b>: stack-based transient swap. <c>Push(item, options)</c> saves
/// <see cref="CurrentItem"/> and equips the new item with the given options for the
/// duration of the push. <c>Pop</c> re-equips the saved item with its original
/// options. <see cref="PushOptions.AsAnimationIndependent"/> overrides
/// <see cref="SyncMode"/> for the duration of the push only.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class VisualSlotNode : Node3D, IVisualNodeProvider
{
    [ExportGroup("Slot Identity")]
    [Export, RequiredExport] public SlotKey Key { get; set; } = null!;

    /// <summary>How this slot's animator participates in composite timing.</summary>
    [Export] public AnimationSyncMode SyncMode { get; set; } = AnimationSyncMode.Slave;

    [ExportGroup("Slot Behavior")]
    /// <summary>If false, <see cref="Unequip"/> reverts to <see cref="DefaultItem"/> (or no-ops).</summary>
    [Export] public bool IsOptional { get; set; } = true;

    /// <summary>Equipped automatically by the composer if the slot is empty after wiring.</summary>
    [Export] public VisualItemData? DefaultItem { get; set; }

    /// <summary>Tags applied to every handle this slot produces (in addition to per-binding tags).</summary>
    [Export] public GCol.Array<StringName> SlotTags { get; set; } = new();

    public VisualItemData? CurrentItem { get; private set; }
    public Node? CurrentInstance { get; private set; }
    public IAnimComponent? Animator { get; private set; }

    public int StackDepth => _stack.Count;
    public bool HasStack => _stack.Count > 0;

    private CompositeAnimatorComponent? _composite;
    private IVisualEffectService? _effects;
    private bool _initialized;

    private readonly Stack<(VisualItemData? item, PushOptions options)> _stack = new();
    private PushOptions _currentOptions = PushOptions.None;

    private readonly List<VisualNodeHandle> _handles = new();

    public event Action<VisualNodeHandle> NodeAdded = delegate { };
    public event Action<VisualNodeHandle> NodeRemoved = delegate { };
    public event Action<VisualNodeHandle> NodeVisibilityChanged = delegate { };

    /// <summary>
    /// Wires the slot's runtime dependencies. Called by <c>VisualComposer._Ready</c>
    /// for every child slot before any default-item equip is performed. Idempotent.
    /// </summary>
    public void Initialize(CompositeAnimatorComponent composite, IVisualEffectService? effects)
    {
        _composite = composite;
        _effects = effects;
        _initialized = true;
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Key == null) { warnings.Add("VisualSlotNode requires a SlotKey resource on Key."); }
        return warnings.ToArray();
    }

    /// <summary>
    /// Equips an item. Atomic: prior instance is fully torn down (with NodeRemoved events
    /// per gone handle) before the new prefab is instantiated and NodeAdded events fire.
    /// </summary>
    public VisualEquipResult Equip(VisualItemData? item)
    {
        if (!_initialized)
        {
            JmoLogger.Error(this, $"VisualSlotNode '{Key?.Id}': Equip before Initialize. Composer must call Initialize on _Ready.");
            return VisualEquipResult.Failed(Key);
        }

        // Defensive: re-instantiate if CurrentItem matches but the prior instance was
        // externally Freed (e.g., test teardown, manual scene-tree manipulation).
        if (CurrentItem == item && CurrentInstance != null && GodotObject.IsInstanceValid(CurrentInstance))
        {
            return CurrentResult(success: true);
        }

        ClearInstance();

        // Equip(null) is a successful unequip, not a failure. Return success with no
        // instance/animator/handles so callers checking Success branch correctly.
        if (item == null)
        {
            return new VisualEquipResult(true, Key, null, null, this, System.Array.Empty<VisualNodeHandle>());
        }

        CurrentItem = item;
        return InstallInstance(item);
    }

    /// <summary>
    /// Unequips the slot. Mandatory slots revert to <see cref="DefaultItem"/> unless
    /// <paramref name="force"/> is true (used internally by atomic swaps). Optional
    /// slots become empty.
    /// </summary>
    public void Unequip(bool force = false)
    {
        if (!IsOptional && !force)
        {
            if (DefaultItem != null && CurrentItem != DefaultItem)
            {
                Equip(DefaultItem);
                return;
            }
            return; // mandatory slot with no default — silently keep current
        }

        ClearInstance();
    }

    /// <summary>
    /// Saves the current item and equips a new one. <see cref="Pop"/> restores the saved
    /// item with its original options. <paramref name="options"/> can override
    /// <see cref="SyncMode"/> for the duration of this push (e.g.
    /// <see cref="PushOptions.AsAnimationIndependent"/>).
    /// </summary>
    public VisualEquipResult Push(VisualItemData item, PushOptions options = PushOptions.None)
    {
        if (!_initialized)
        {
            JmoLogger.Error(this, $"VisualSlotNode '{Key?.Id}': Push before Initialize.");
            return VisualEquipResult.Failed(Key);
        }

        _stack.Push((CurrentItem, _currentOptions));
        _currentOptions = options;
        ClearInstance();
        CurrentItem = item;
        return InstallInstance(item);
    }

    /// <summary>
    /// Restores the most recently pushed item with its original options. If the stack is
    /// empty, behaves like <see cref="Unequip"/> — mandatory slot reverts to default,
    /// optional slot becomes empty.
    /// </summary>
    public void Pop()
    {
        if (!_initialized) { return; }

        if (_stack.Count == 0)
        {
            _currentOptions = PushOptions.None;
            Unequip();
            return;
        }

        var (prevItem, prevOptions) = _stack.Pop();
        ClearInstance();
        _currentOptions = prevOptions;
        if (prevItem != null)
        {
            CurrentItem = prevItem;
            InstallInstance(prevItem);
        }
        else
        {
            // Saved state was empty — leave slot cleared (or fall back to default for mandatory).
            if (!IsOptional && DefaultItem != null)
            {
                CurrentItem = DefaultItem;
                InstallInstance(DefaultItem);
            }
        }
    }

    private VisualEquipResult InstallInstance(VisualItemData item)
    {
        if (item.Prefab == null)
        {
            JmoLogger.Error(this, $"VisualSlotNode '{Key?.Id}': item '{item.Id}' has no Prefab.");
            CurrentItem = null;
            return VisualEquipResult.Failed(Key);
        }

        CurrentInstance = item.Prefab.Instantiate();
        AddChild(CurrentInstance);

        ApplyOverrides(CurrentInstance, item);

        // Build handles BEFORE registering the animator with the composite. If
        // BuildHandles ever throws (e.g., a future hardening promotes a missing rig
        // binding from Warning to exception), the slot stays cleanly torn-down rather
        // than half-installed with the composite holding a dangling animator reference.
        BuildHandles(CurrentInstance, item);

        Animator = GetAnimComponent(CurrentInstance);
        if (Animator != null && ShouldRegisterWithComposite())
        {
            _composite?.RegisterAnimator(Animator, isMaster: SyncMode == AnimationSyncMode.Master);
        }

        // Fire NodeAdded after handles are populated so subscribers see the full set.
        foreach (var h in _handles)
        {
            NodeAdded?.Invoke(h);
        }

        return CurrentResult(success: true);
    }

    private void ClearInstance()
    {
        if (_handles.Count > 0)
        {
            // Unregister base colors first (effect service-side bookkeeping)
            foreach (var h in _handles)
            {
                _effects?.UnregisterSprite(h.Node);
            }
            // Fire NodeRemoved AFTER handles list is cleared so subscribers querying
            // GetVisualNodes() inside the handler see the post-removal state.
            var removed = _handles.ToArray();
            _handles.Clear();
            foreach (var h in removed)
            {
                NodeRemoved?.Invoke(h);
            }
        }

        if (Animator != null && ShouldRegisterWithComposite())
        {
            // stopFirst: false — the underlying node is about to be QueueFree'd.
            _composite?.UnregisterAnimator(Animator, stopFirst: false);
        }
        Animator = null;

        if (CurrentInstance != null && GodotObject.IsInstanceValid(CurrentInstance))
        {
            CurrentInstance.QueueFree();
        }
        CurrentInstance = null;
        CurrentItem = null;
    }

    private bool ShouldRegisterWithComposite()
    {
        if (SyncMode == AnimationSyncMode.Independent) { return false; }
        if (_currentOptions.HasFlag(PushOptions.AsAnimationIndependent)) { return false; }
        return true;
    }

    private VisualEquipResult CurrentResult(bool success)
        => new(success, Key, CurrentInstance, Animator, this, _handles.ToArray());

    private void BuildHandles(Node prefabRoot, VisualItemData item)
    {
        _handles.Clear();

        if (item.Rig != null && item.Rig.Bindings.Count > 0)
        {
            foreach (var binding in item.Rig.Bindings)
            {
                var node = prefabRoot.GetNodeOrNull(binding.TargetNode);
                if (node == null)
                {
                    JmoLogger.Warning(this, $"VisualSlotNode '{Key?.Id}': rig binding path '{binding.TargetNode}' not found in prefab '{item.Id}'.");
                    continue;
                }
                var tags = MergeTags(SlotTags, binding.Tags);
                _handles.Add(new VisualNodeHandle(
                    Key, binding.PartId, tags, node, this, IsNodeVisible(node)));
            }
        }
        else
        {
            // Fallback: recursive sprite walk. Tagless+partless except slot's own tags.
            var nodes = new List<Node>();
            VisualNodeAggregator.CollectSprites(prefabRoot, nodes);
            var tags = MergeTags(SlotTags, null);
            foreach (var n in nodes)
            {
                _handles.Add(new VisualNodeHandle(
                    Key, null, tags, n, this, IsNodeVisible(n)));
            }
        }
    }

    private static IReadOnlySet<StringName> MergeTags(GCol.Array<StringName> a, GCol.Array<StringName>? b)
    {
        var set = new HashSet<StringName>();
        foreach (var t in a) { set.Add(t); }
        if (b != null) { foreach (var t in b) { set.Add(t); } }
        return set;
    }

    private static bool IsNodeVisible(Node n)
    {
        if (n is Node3D n3d) { return n3d.Visible; }
        if (n is CanvasItem ci) { return ci.Visible; }
        return true;
    }

    private void ApplyOverrides(Node instance, VisualItemData item)
    {
        // With rig: apply per-binding (only bindings flagged ReceivesXOverride).
        if (item.Rig != null && item.Rig.Bindings.Count > 0)
        {
            foreach (var binding in item.Rig.Bindings)
            {
                var node = instance.GetNodeOrNull(binding.TargetNode);
                if (node == null) { continue; }
                ApplyOverridesToNode(node, item, binding);
            }
            return;
        }

        // Fallback: first sprite found wins (legacy behavior).
        var sprite2D = instance as Sprite2D;
        if (sprite2D != null || instance.TryGetFirstChildOfType<Sprite2D>(out sprite2D))
        {
            ApplyOverridesToSprite2D(sprite2D!, item);
            return;
        }

        var sprite3D = instance as Sprite3D;
        if (sprite3D != null || instance.TryGetFirstChildOfType<Sprite3D>(out sprite3D))
        {
            ApplyOverridesToSprite3D(sprite3D!, item);
        }
    }

    private void ApplyOverridesToNode(Node node, VisualItemData item, VisualPartBinding binding)
    {
        if (node is Sprite2D s2)
        {
            if (binding.ReceivesTextureOverride && item.TextureOverride != null) { s2.Texture = item.TextureOverride; }
            if (binding.ReceivesRowOverride && item.SpriteSheetRowOverride >= 0)
            {
                s2.FrameCoords = new Vector2I(s2.FrameCoords.X, item.SpriteSheetRowOverride);
            }
            ApplyModulate(s2, item, binding);
            return;
        }
        if (node is Sprite3D s3)
        {
            if (binding.ReceivesTextureOverride && item.TextureOverride != null) { s3.Texture = item.TextureOverride; }
            if (binding.ReceivesRowOverride && item.SpriteSheetRowOverride >= 0)
            {
                s3.FrameCoords = new Vector2I(s3.FrameCoords.X, item.SpriteSheetRowOverride);
            }
            ApplyModulate(s3, item, binding);
            return;
        }
        // Other node types — only modulate path applies if it's a CanvasItem/SpriteBase3D
        if (node is SpriteBase3D sb3)
        {
            ApplyModulate(sb3, item, binding);
            return;
        }
        if (node is CanvasItem ci)
        {
            ApplyModulate(ci, item, binding);
        }
    }

    private void ApplyModulate(GodotObject target, VisualItemData item, VisualPartBinding binding)
    {
        if (!binding.ReceivesModulateOverride) { return; }
        var color = item.ModulateOverride;
        switch (target)
        {
            case Sprite2D s2: s2.Modulate = color; break;
            case CanvasItem ci: ci.Modulate = color; break;
            case SpriteBase3D s3d: s3d.Modulate = color; break;
        }
        if (target is Node n) { _effects?.RegisterBaseColor(n, color); }
    }

    private void ApplyOverridesToSprite2D(Sprite2D sprite, VisualItemData item)
    {
        if (item.TextureOverride != null) { sprite.Texture = item.TextureOverride; }
        if (item.SpriteSheetRowOverride >= 0)
        {
            sprite.FrameCoords = new Vector2I(sprite.FrameCoords.X, item.SpriteSheetRowOverride);
        }
        var color = item.ModulateOverride;
        sprite.Modulate = color;
        _effects?.RegisterBaseColor(sprite, color);
    }

    private void ApplyOverridesToSprite3D(Sprite3D sprite, VisualItemData item)
    {
        if (item.TextureOverride != null) { sprite.Texture = item.TextureOverride; }
        if (item.SpriteSheetRowOverride >= 0)
        {
            sprite.FrameCoords = new Vector2I(sprite.FrameCoords.X, item.SpriteSheetRowOverride);
        }
        var color = item.ModulateOverride;
        sprite.Modulate = color;
        _effects?.RegisterBaseColor(sprite, color);
    }

    private static IAnimComponent? GetAnimComponent(Node node)
    {
        if (node is IAnimComponent anim) { return anim; }
        if (node.TryGetFirstChildOfInterface<IAnimComponent>(out var childAnim))
        {
            return childAnim;
        }
        return null;
    }

    #region IVisualNodeProvider

    public IReadOnlyList<VisualNodeHandle> GetVisualNodes(VisualQuery query)
    {
        var results = new List<VisualNodeHandle>();
        foreach (var h in _handles)
        {
            if (query.Matches(h)) { results.Add(h); }
        }
        return results;
    }

    public IReadOnlyList<VisualNodeHandle> GetVisibleNodes(VisualQuery query)
    {
        var results = new List<VisualNodeHandle>();
        foreach (var h in _handles)
        {
            // Re-check visibility live — handle's IsVisible is captured at build time
            // and may be stale if a coordinator toggled the node's Visible flag.
            var live = h with { IsVisible = IsNodeVisible(h.Node) };
            if (live.IsVisible && query.Matches(live)) { results.Add(live); }
        }
        return results;
    }

    #endregion
}
