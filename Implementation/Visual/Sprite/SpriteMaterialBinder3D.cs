namespace Jmodot.Implementation.Visual.Sprite;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Implementation.Shared.GodotExceptions;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Effects;
using Jmodot.Core.Visual.Sprite;
using Shared;

/// <summary>Binds runtime sprite materials to sprites under its parent using the required project shader body.</summary>
[GlobalClass, Tool]
public partial class SpriteMaterialBinder3D : Node
{
    /// <summary>The shader body shared by this entity's sprites; required for binding.</summary>
    [Export, RequiredExport] public ShaderInclude Body { get; set; } = null!;

    private readonly HashSet<SpriteBase3D> _bound = new();
    private readonly HashSet<SpriteBase3D> _reported = new();
    private readonly Dictionary<SpriteBase3D, (SpriteShaderVariant Variant, string BodyPath, Texture2D? Texture, Vector4 Rect)> _bindings = new();
    private readonly Dictionary<SpriteBase3D, List<ISpriteMaterialContributor>> _scopes = new();
    private static readonly Dictionary<(string Path, SpriteShaderVariant Variant), Shader> VariantShaders = new();
    private bool _departureQueued;
    private bool _duplicateReported;
    private IVisualNodeProvider? _provider;

    public IReadOnlyCollection<SpriteBase3D> Bound => _bound;

    public const string BoundMeta = "jmodot_sprite_material_binder";
    public const string TextureFilterMacro = "SPRITE_TEXTURE_FILTER";

    public static class Uniform
    {
        public static readonly StringName Texture = "sprite_texture";
        public static readonly StringName FrameRect = "sprite_frame_rect";
        public static readonly StringName AlphaScissorThreshold = "alpha_scissor_threshold";
        public static readonly StringName AlphaHashScale = "alpha_hash_scale";
    }

    public override void _Ready()
    {
        // The editor's mechanism is the configuration warning: a throw here would abort _Ready in the editor,
        // before the preview the binder exists to give.
        if (!Engine.IsEditorHint())
        {
            this.ValidateRequiredExports();
            if (string.IsNullOrWhiteSpace(Body.ResourcePath))
            {
                throw new NodeConfigurationException("Sprite material Body needs a loadable ResourcePath.", this);
            }
        }
        Resync();
        SubscribeProvider();
        ConnectMembership();
        ReportDuplicateSlots();
        if (!Engine.IsEditorHint() && GetParent() != null
            && VisualNodeAggregator.CollectProviders(GetParent()).Count > 1)
        {
            JmoLogger.Error(this, "Sprite material binder has more than one outermost visual provider; none is subscribed.");
        }
    }

    public override void _EnterTree()
    {
        if (!IsNodeReady()) { return; }
        foreach (var sprite in _bound.ToArray())
        {
            if (!GodotObject.IsInstanceValid(sprite)) { Unbind(sprite); continue; }
            ConnectSignals(sprite);
        }
        SubscribeProvider();
        ConnectMembership();
    }

    public override void _ExitTree()
    {
        UnsubscribeProvider();
        foreach (var sprite in _bound.ToArray())
        {
            if (!GodotObject.IsInstanceValid(sprite)) { Unbind(sprite); continue; }
            DisconnectSignals(sprite);
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        if (Body == null) { warnings.Add("Sprite material binder requires Body."); }
        else if (string.IsNullOrWhiteSpace(Body.ResourcePath))
        {
            warnings.Add("Sprite material Body needs a loadable path.");
        }
        if (GetParent() == null) { return warnings.ToArray(); }
        foreach (var sprite in VisualNodeAggregator.CollectSprites(GetParent()).OfType<SpriteBase3D>())
        {
            if (FindCovering(sprite, GetParent()) != this) { continue; }
            if (sprite.MaterialOverride != null && !IsBoundMaterial(sprite.MaterialOverride))
            {
                warnings.Add($"Sprite '{sprite.Name}' has an authored material override; the binder cannot bind it.");
            }
            string? missing = FindUnreproducedFlag(sprite);
            if (missing != null) { warnings.Add($"Sprite '{sprite.Name}' uses {missing}, which the bound material cannot reproduce."); }
        }
        if (VisualNodeAggregator.CollectProviders(GetParent()).Count > 1)
        {
            warnings.Add("More than one outermost visual provider exists; remove the ambiguity.");
        }
        if (HasDuplicateSlot())
        {
            warnings.Add("Two contributors of one slot are children of this binder; the first in child order applies.");
        }
        return warnings.ToArray();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationEditorPreSave)
        {
            foreach (var sprite in _bound)
            {
                if (GodotObject.IsInstanceValid(sprite) && IsBoundMaterial(sprite.MaterialOverride))
                {
                    sprite.MaterialOverride = null;
                }
            }
        }
        if (what == NotificationEditorPostSave)
        {
            foreach (var sprite in _bound.ToArray()) { Rebuild(sprite); }
        }
    }

    private static string? FindUnreproducedFlag(SpriteBase3D sprite)
    {
        if (sprite.Get("alpha_antialiasing_mode").AsInt32() != 0) { return "alpha antialiasing"; }
        Texture2D? frame = sprite switch
        {
            Sprite3D still => still.Texture,
            AnimatedSprite3D animated => animated.SpriteFrames?.GetFrameTexture(animated.Animation, animated.Frame),
            _ => null,
        };
        if (frame is AtlasTexture atlas && atlas.Margin != default) { return "an atlas margin"; }
        if (sprite is Sprite3D stillRegion && stillRegion.RegionEnabled && stillRegion.Texture != null)
        {
            var region = stillRegion.RegionRect;
            var size = stillRegion.Texture.GetSize();
            if (region.Position.X < 0f || region.Position.Y < 0f
                || region.End.X > size.X || region.End.Y > size.Y) { return "a region outside its texture"; }
        }
        return null;
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) { Resync(); }
    }

    public void Resync()
    {
        // Without a loadable Body there is no include to compile, so the editor binds nothing and warns.
        if (GetParent() == null || string.IsNullOrWhiteSpace(Body?.ResourcePath)) { return; }
        var found = VisualNodeAggregator.CollectSprites(GetParent()).OfType<SpriteBase3D>()
            .Where(sprite => FindCovering(sprite, GetParent()) == this).ToHashSet();
        foreach (var sprite in _bound.ToArray())
        {
            if (!GodotObject.IsInstanceValid(sprite) || !found.Contains(sprite)) { Unbind(sprite); }
        }
        foreach (var sprite in found)
        {
            if (!_bound.Contains(sprite)) { Bind(sprite); continue; }
            if (!IsBoundMaterial(sprite.MaterialOverride)) { Unbind(sprite); continue; }
            var variant = SpriteShaderVariant.Of(sprite);
            var previous = _bindings[sprite];
            if (previous.Variant != variant || previous.BodyPath != Body.ResourcePath)
            {
                Rebuild(sprite);
                continue;
            }
            SyncFrame(sprite);
        }
    }

    public static bool IsBoundMaterial(Material? material)
        => material != null && material.HasMeta(BoundMeta);

    /// <summary>Returns the nearest binder covering a node, stopping at the given root.</summary>
    public static SpriteMaterialBinder3D? FindCovering(Node node, Node stopAt)
    {
        for (Node? ancestor = node; ancestor != null; ancestor = ancestor.GetParent())
        {
            foreach (var child in ancestor.GetChildren())
            {
                if (child is SpriteMaterialBinder3D binder) { return binder; }
            }
            if (ancestor == stopAt) { break; }
        }
        return null;
    }

    /// <summary>Rebuilds the material of every sprite in the contributor's scope, so a changed contribution
    /// leaves no stale uniform: the parent binder's whole bound set, or the contributor's parent sprite alone.
    /// A no-op when the scope is unbound or leaving the tree.</summary>
    public static void RefreshContributions(Node contributor)
    {
        if (contributor == null || !GodotObject.IsInstanceValid(contributor)
            || !contributor.IsInsideTree() || contributor.GetParent() == null) { return; }
        if (contributor.GetParent() is SpriteBase3D sprite)
        {
            var covering = FindCovering(sprite, contributor.GetTree()!.Root);
            if (covering != null && covering._bound.Contains(sprite)) { covering.Rebuild(sprite); }
            return;
        }
        if (contributor.GetParent() is SpriteMaterialBinder3D owning) { owning.RebuildAll(); return; }
        FindCovering(contributor, contributor.GetTree()!.Root)?.RebuildAll();
    }

    private List<ISpriteMaterialContributor> ApplyContributions(SpriteBase3D sprite, ShaderMaterial material, Node? excluding)
    {
        var scope = ScopeOf(sprite, excluding);
        foreach (var contributor in scope) { contributor.ContributeTo(sprite, material); }
        return scope;
    }

    // The sprite's own contributors first, then the binder's for every slot they leave free: the whole of a
    // sprite's slot replaces the binder's slot on that sprite. Within one parent the first contributor of a slot wins.
    private List<ISpriteMaterialContributor> ScopeOf(SpriteBase3D sprite, Node? excluding)
    {
        var scope = new List<ISpriteMaterialContributor>();
        var slots = new HashSet<StringName>();
        foreach (var contributor in sprite.GetChildren().OfType<ISpriteMaterialContributor>())
        {
            if (ReferenceEquals(contributor, excluding)) { continue; }
            if (slots.Add(contributor.Slot)) { scope.Add(contributor); }
        }
        foreach (var contributor in GetChildren().OfType<ISpriteMaterialContributor>())
        {
            if (ReferenceEquals(contributor, excluding)) { continue; }
            if (slots.Add(contributor.Slot)) { scope.Add(contributor); }
        }
        return scope;
    }

    private bool HasDuplicateSlot()
    {
        var slots = new HashSet<StringName>();
        foreach (var contributor in GetChildren().OfType<ISpriteMaterialContributor>())
        {
            if (!slots.Add(contributor.Slot)) { return true; }
        }
        return false;
    }

    private void ReportDuplicateSlots()
    {
        if (_duplicateReported || Engine.IsEditorHint() || !HasDuplicateSlot()) { return; }
        _duplicateReported = true;
        JmoLogger.Error(this, "Two contributors of one slot are children of this binder; the first in child order applies.");
    }

    private void ConnectMembership()
    {
        var entered = new Callable(this, MethodName.OnChildEntered);
        var exiting = new Callable(this, MethodName.OnChildExiting);
        if (!IsConnected(Node.SignalName.ChildEnteredTree, entered)) { Connect(Node.SignalName.ChildEnteredTree, entered); }
        if (!IsConnected(Node.SignalName.ChildExitingTree, exiting)) { Connect(Node.SignalName.ChildExitingTree, exiting); }
    }

    private void OnChildEntered(Node child)
    {
        if (!IsInsideTree()) { return; }
        ReportDuplicateSlots();
        if (child is not ISpriteMaterialContributor) { return; }
        if (child.GetParent() is SpriteBase3D sprite)
        {
            if (_bound.Contains(sprite)) { Rebuild(sprite); }
            return;
        }
        RebuildAll();
    }

    // child_exiting_tree fires while the departing child is still among its parent's children.
    private void OnChildExiting(Node child)
    {
        if (!IsInsideTree()) { return; }
        if (child is not ISpriteMaterialContributor) { return; }
        if (child.GetParent() is SpriteBase3D sprite)
        {
            if (_bound.Contains(sprite)) { Rebuild(sprite, child); }
            return;
        }
        RebuildAll(child);
    }

    #region Test Helpers
#if TOOLS
    /// <summary>Re-derives a bound sprite's variant, texture, frame rect and contributions.</summary>
    public void Rebind(SpriteBase3D sprite) => Rebuild(sprite);
#endif
    #endregion

    private void Rebuild(SpriteBase3D sprite, Node? excluding = null)
    {
        if (!GodotObject.IsInstanceValid(sprite) || !_bound.Contains(sprite)) { return; }
        Unbind(sprite);
        Bind(sprite, excluding);
    }

    private void RebuildAll(Node? excluding = null)
    {
        foreach (var sprite in _bound.ToArray()) { Rebuild(sprite, excluding); }
    }

    private void SubscribeProvider()
    {
        if (Engine.IsEditorHint() || GetParent() == null) { return; }
        var providers = VisualNodeAggregator.CollectProviders(GetParent());
        if (providers.Count != 1) { return; }
        if (ReferenceEquals(_provider, providers[0])) { return; }
        UnsubscribeProvider();
        _provider = providers[0];
        _provider.NodeAdded += OnProviderAdded;
        _provider.NodeRemoved += OnProviderRemoved;
    }

    private void UnsubscribeProvider()
    {
        if (_provider == null) { return; }
        _provider.NodeAdded -= OnProviderAdded;
        _provider.NodeRemoved -= OnProviderRemoved;
        _provider = null;
    }

    private void OnProviderAdded(VisualNodeHandle handle)
    {
        if (handle.Node is SpriteBase3D sprite && GetParent() != null
            && FindCovering(sprite, GetParent()) == this && !_bound.Contains(sprite)) { Bind(sprite); }
    }

    private void OnProviderRemoved(VisualNodeHandle handle)
    {
        if (handle.Node is SpriteBase3D sprite && _bound.Contains(sprite)) { Unbind(sprite); }
    }

    private void Bind(SpriteBase3D sprite, Node? excluding = null)
    {
        if (sprite.MaterialOverride != null)
        {
            ReportConfiguration(sprite, "an authored material override; the binder cannot bind it");
            return;
        }
        string? missing = FindUnreproducedFlag(sprite);
        if (missing != null) { ReportConfiguration(sprite, missing); }
        var variant = SpriteShaderVariant.Of(sprite);
        var key = (Body.ResourcePath, variant with { RenderPriority = 0 });
        if (!VariantShaders.TryGetValue(key, out var shader))
        {
            shader = new Shader { Code = variant.Code(Body.ResourcePath) };
            VariantShaders[key] = shader;
        }
        var material = new ShaderMaterial { Shader = shader, RenderPriority = variant.RenderPriority };
        material.SetMeta(BoundMeta, true);
        var (sampled, rect, _) = SpriteFrameRect.Of(sprite);
        material.SetShaderParameter(Uniform.Texture, sampled);
        material.SetShaderParameter(Uniform.FrameRect, rect);
        material.SetShaderParameter(Uniform.AlphaScissorThreshold, sprite.AlphaScissorThreshold);
        material.SetShaderParameter(Uniform.AlphaHashScale, sprite.AlphaHashScale);
        _scopes[sprite] = ApplyContributions(sprite, material, excluding);
        sprite.MaterialOverride = material;
        _bound.Add(sprite);
        _bindings[sprite] = (variant, Body.ResourcePath, sampled, rect);
        ConnectSignals(sprite);
    }

    private void ReportConfiguration(SpriteBase3D sprite, string problem)
    {
        if (Engine.IsEditorHint() || !_reported.Add(sprite)) { return; }
        JmoLogger.Error(this, $"Sprite '{sprite.Name}' uses {problem}.");
    }

    private void SyncFrame(SpriteBase3D sprite)
    {
        if (sprite.MaterialOverride is not ShaderMaterial material) { return; }
        var (sampled, rect, _) = SpriteFrameRect.Of(sprite);
        var prior = _bindings[sprite];
        if (prior.Texture != sampled) { material.SetShaderParameter(Uniform.Texture, sampled); }
        if (prior.Rect != rect) { material.SetShaderParameter(Uniform.FrameRect, rect); }
        _bindings[sprite] = prior with { Texture = sampled, Rect = rect };
    }

    private void Unbind(SpriteBase3D sprite)
    {
        _bound.Remove(sprite);
        _bindings.Remove(sprite);
        _scopes.Remove(sprite);
        if (!GodotObject.IsInstanceValid(sprite)) { return; }
        DisconnectSignals(sprite);
        if (IsBoundMaterial(sprite.MaterialOverride)) { sprite.MaterialOverride = null; }
    }

    private void ConnectSignals(SpriteBase3D sprite)
    {
        var changed = new Callable(this, MethodName.OnSpriteChanged);
        var exiting = new Callable(this, MethodName.OnSpriteExiting);
        if (!sprite.IsConnected(Node.SignalName.TreeExiting, exiting)) { sprite.Connect(Node.SignalName.TreeExiting, exiting); }
        var entered = new Callable(this, MethodName.OnChildEntered);
        var childExiting = new Callable(this, MethodName.OnChildExiting);
        if (!sprite.IsConnected(Node.SignalName.ChildEnteredTree, entered)) { sprite.Connect(Node.SignalName.ChildEnteredTree, entered); }
        if (!sprite.IsConnected(Node.SignalName.ChildExitingTree, childExiting)) { sprite.Connect(Node.SignalName.ChildExitingTree, childExiting); }
        if (sprite is Sprite3D still)
        {
            if (!still.IsConnected(Sprite3D.SignalName.FrameChanged, changed)) { still.Connect(Sprite3D.SignalName.FrameChanged, changed); }
            if (!still.IsConnected(Sprite3D.SignalName.TextureChanged, changed)) { still.Connect(Sprite3D.SignalName.TextureChanged, changed); }
        }
        if (sprite is AnimatedSprite3D animated)
        {
            if (!animated.IsConnected(AnimatedSprite3D.SignalName.FrameChanged, changed)) { animated.Connect(AnimatedSprite3D.SignalName.FrameChanged, changed); }
            if (!animated.IsConnected(AnimatedSprite3D.SignalName.AnimationChanged, changed)) { animated.Connect(AnimatedSprite3D.SignalName.AnimationChanged, changed); }
            if (!animated.IsConnected(AnimatedSprite3D.SignalName.SpriteFramesChanged, changed)) { animated.Connect(AnimatedSprite3D.SignalName.SpriteFramesChanged, changed); }
        }
    }

    private void DisconnectSignals(SpriteBase3D sprite)
    {
        var changed = new Callable(this, MethodName.OnSpriteChanged);
        var exiting = new Callable(this, MethodName.OnSpriteExiting);
        if (sprite.IsConnected(Node.SignalName.TreeExiting, exiting)) { sprite.Disconnect(Node.SignalName.TreeExiting, exiting); }
        var entered = new Callable(this, MethodName.OnChildEntered);
        var childExiting = new Callable(this, MethodName.OnChildExiting);
        if (sprite.IsConnected(Node.SignalName.ChildEnteredTree, entered)) { sprite.Disconnect(Node.SignalName.ChildEnteredTree, entered); }
        if (sprite.IsConnected(Node.SignalName.ChildExitingTree, childExiting)) { sprite.Disconnect(Node.SignalName.ChildExitingTree, childExiting); }
        if (sprite is Sprite3D still)
        {
            if (still.IsConnected(Sprite3D.SignalName.FrameChanged, changed)) { still.Disconnect(Sprite3D.SignalName.FrameChanged, changed); }
            if (still.IsConnected(Sprite3D.SignalName.TextureChanged, changed)) { still.Disconnect(Sprite3D.SignalName.TextureChanged, changed); }
        }
        if (sprite is AnimatedSprite3D animated)
        {
            if (animated.IsConnected(AnimatedSprite3D.SignalName.FrameChanged, changed)) { animated.Disconnect(AnimatedSprite3D.SignalName.FrameChanged, changed); }
            if (animated.IsConnected(AnimatedSprite3D.SignalName.AnimationChanged, changed)) { animated.Disconnect(AnimatedSprite3D.SignalName.AnimationChanged, changed); }
            if (animated.IsConnected(AnimatedSprite3D.SignalName.SpriteFramesChanged, changed)) { animated.Disconnect(AnimatedSprite3D.SignalName.SpriteFramesChanged, changed); }
        }
    }

    private void OnSpriteChanged()
    {
        foreach (var sprite in _bound.ToArray())
        {
            if (!GodotObject.IsInstanceValid(sprite)) { Unbind(sprite); continue; }
            SyncFrame(sprite);
        }
    }

    private void OnSpriteExiting()
    {
        if (_departureQueued) { return; }
        _departureQueued = true;
        CallDeferred(MethodName.CheckDepartures);
    }

    private void CheckDepartures()
    {
        _departureQueued = false;
        // The check tests ownership rather than position: a binder outside the tree keeps its bindings for
        // _EnterTree to reconnect, as _ExitTree intends, and ownership is only decidable from inside the tree.
        if (GetTree() is not { } tree) { return; }
        foreach (var sprite in _bound.ToArray())
        {
            if (!GodotObject.IsInstanceValid(sprite) || FindCovering(sprite, tree.Root) != this) { Unbind(sprite); }
        }
    }
}
