// Intentionally in the global namespace for extension method discoverability.
// 93+ files use these extensions without explicit imports. Do not add a namespace declaration.

#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot.Collections;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Identification;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

#endregion

public static class NodeExts
{
    #region VALIDATION_EXTENSIONS

    public static bool IsValid<T>(this T node) where T : GodotObject
    {
        return node is not null
               && GodotObject.IsInstanceValid(node)
               && !node.IsQueuedForDeletion();
    }

    /// <summary>
    ///     Extension that checks if the object is valid to use. See the "IsValid" extension for more information.
    /// </summary>
    public static T? IfValid<T>(this T control) where T : GodotObject
    {
        return control.IsValid() ? control : null;
    }

    public static IBlackboardGraph? GetGraph(this Node node)
        => node.TryGetFirstChildOfType<BlackboardGraph>(out var graph) ? graph : null;

    /// <summary>
    /// Returns the BlackboardGraph that owns this Blackboard, or null if none is wired.
    /// Walks one node-tree hop (Blackboard → its parent), so cross-scope reads silently
    /// degrade to local-only when no graph is configured (test fixtures, standalone use).
    /// </summary>
    public static IBlackboardGraph? FindParentGraph(this IBlackboard bb)
        => (bb as Node)?.GetParent() as IBlackboardGraph;

    public static void SafeQueueFree(this Node node)
    {
        if (node.IsValid())
        {
            node.QueueFree();
        }
        else
        {
            JmoLogger.Warning(typeof(NodeExts), "Couldn't safely queue-free node: node is not valid or already freed");
        }
    }

    /// <summary>
    /// Validates that all properties and fields marked with [RequiredExport] are not null.
    /// Call this in _Ready() to fail-fast with a clear error if any required exports are missing.
    /// </summary>
    /// <exception cref="NodeConfigurationException">
    /// Thrown when a [RequiredExport] property or field is null.
    /// </exception>
    /// <example>
    /// <code>
    /// [Export, RequiredExport] public SpellArchetype Archetype { get; set; } = null!;
    ///
    /// public override void _Ready()
    /// {
    ///     this.ValidateRequiredExports();
    /// }
    /// </code>
    /// </example>
    public static void ValidateRequiredExports(this Node node)
    {
        foreach (var member in RequiredExportMembers.For(node.GetType()))
        {
            if (member.GetValue(node) == null)
            {
                var ownerName = node.Owner?.Name ?? "[no owner]";
                throw new NodeConfigurationException(
                    $"Required export '{member.Name}' must be assigned in the Inspector for scene owner {ownerName}.", node);
            }
        }
    }

    public static void DisableProcessing(this Node node)
    {
        node.SetProcess(false);
        node.SetPhysicsProcess(false);

        if (node is Node2D node2D)
        {
            node2D.SetProcessInput(false);
            node2D.SetProcessUnhandledInput(false);
        }
        else if (node is Node3D node3D)
        {
            node3D.SetProcessInput(false);
            node3D.SetProcessUnhandledInput(false);
        }
    }

    /// <summary>
    /// Orphan-safety net for self-freeing transients: after <paramref name="seconds"/>, if the
    /// node is still valid and <paramref name="unless"/> (when supplied) does not report true,
    /// queue-frees it. Pairs with — never replaces — the node's primary completion path (e.g. an
    /// event-driven finish); this is the fallback for when that path never fires.
    /// </summary>
    public static void FreeAfterTimeout(this Node node, float seconds, Func<bool>? unless = null)
    {
        GameClock.CreateTimer(node, seconds).Timeout += () =>
        {
            if (!node.IsValid())
            {
                return;
            }

            if (unless != null && unless())
            {
                return;
            }

            node.QueueFree();
        };
    }

    /// <summary>
    /// Calls <paramref name="onRemoved"/> once when <paramref name="node"/> has left the tree for good,
    /// never on a reparent. <c>TreeExiting</c> fires for a reparent as well, so the decision is deferred
    /// to the next idle callback, where only a node that is freed or still outside the tree counts as
    /// removed. The watch ends after that one call; a node that is removed but kept alive and parented
    /// again needs a new watch.
    /// </summary>
    /// <returns>An action that ends the watch before it fires; safe to call after the node is freed.</returns>
    public static Action OnRemovedFromTree(this Node node, Action onRemoved)
    {
        var fired = false;

        void Exiting() => Callable.From(Confirm).CallDeferred();

        void Confirm()
        {
            if (fired) { return; }
            if (GodotObject.IsInstanceValid(node))
            {
                if (node.IsInsideTree()) { return; }
                node.TreeExiting -= Exiting;
            }

            fired = true;
            onRemoved();
        }

        node.TreeExiting += Exiting;
        return () =>
        {
            fired = true;
            if (GodotObject.IsInstanceValid(node)) { node.TreeExiting -= Exiting; }
        };
    }

    /// <summary>
    /// Subscribes <paramref name="handler"/> to <paramref name="source"/> now, and keeps the subscription
    /// only while <paramref name="owner"/> is in the tree: it unsubscribes on the owner's exit and
    /// subscribes again on its entry.
    /// </summary>
    /// <remarks>
    /// A call with the same owner, source and handler changes nothing, so an initializer that runs again on a
    /// pooled reuse binds once. A source freed before the owner exits is not unsubscribed, because its events are
    /// gone with it and teardown frees siblings in no fixed order; the next bind on the owner drops its binding.
    /// A binding is never removed while its source lives, so a caller that replaces a live source with another
    /// keeps both subscribed and needs a hand-written pair instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handler"/> is a lambda or local function, not a method
    /// group: it is a new delegate on every call, so a repeated bind could not match it and would subscribe again.</exception>
    public static void BindWhileInTree<TSource, THandler>(this Node owner, TSource source, THandler handler,
        Action<TSource, THandler> subscribe, Action<TSource, THandler> unsubscribe)
        where TSource : class where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(source);
        TreeBindingRegistry.For(owner).Add(source, handler,
            () => subscribe(source, handler), () => unsubscribe(source, handler));
    }

    /// <summary>
    /// The static-event form of <see cref="BindWhileInTree{TSource,THandler}"/>: the same owner-tree lifetime
    /// and the same per-(owner, handler) idempotence, for an event with no source object to free.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="handler"/> is a lambda or local function, not a method group.</exception>
    public static void BindStaticWhileInTree<THandler>(this Node owner, THandler handler,
        Action<THandler> subscribe, Action<THandler> unsubscribe) where THandler : Delegate
    {
        TreeBindingRegistry.For(owner).Add(null, handler,
            () => subscribe(handler), () => unsubscribe(handler));
    }

    private static readonly ConditionalWeakTable<Node, TreeBindingRegistry> TreeRegistries = new();

    #region Test Helpers
#if TOOLS
    /// <summary>The number of tree bindings <paramref name="owner"/> holds, for tests.</summary>
    internal static int _TestTreeBindingCount(Node owner)
        => TreeRegistries.TryGetValue(owner, out var registry) ? registry.Count : 0;
#endif
    #endregion

    private sealed class TreeBindingRegistry
    {
        private readonly List<TreeBinding> _bindings = [];

        private TreeBindingRegistry(Node owner)
        {
            owner.TreeExiting += OnOwnerExiting;
            owner.TreeEntered += OnOwnerEntered;
        }

        public static TreeBindingRegistry For(Node owner) => TreeRegistries.GetValue(owner, o => new TreeBindingRegistry(o));

        public int Count => _bindings.Count;

        public void Add(object? source, Delegate handler, Action subscribe, Action unsubscribe)
        {
            if (handler.Method.IsDefined(typeof(CompilerGeneratedAttribute), false)
                || handler.Method.DeclaringType?.IsDefined(typeof(CompilerGeneratedAttribute), false) == true)
            {
                throw new ArgumentException("A tree binding needs a method-group handler: a lambda or local function is a new delegate on every call, so a repeated bind would subscribe it again.", nameof(handler));
            }

            _bindings.RemoveAll(b => !b.SourceLive);
            if (_bindings.Exists(b => b.Matches(source, handler))) { return; }
            var binding = new TreeBinding(source, handler, subscribe, unsubscribe);
            _bindings.Add(binding);
            binding.Arm();
        }

        private void OnOwnerExiting()
        {
            foreach (var binding in _bindings) { binding.Disarm(); }
        }

        private void OnOwnerEntered()
        {
            foreach (var binding in _bindings) { binding.Arm(); }
        }
    }

    private sealed class TreeBinding(object? source, Delegate handler, Action subscribe, Action unsubscribe)
    {
        private bool _armed;

        public bool Matches(object? otherSource, Delegate otherHandler)
            => ReferenceEquals(source, otherSource) && handler.Equals(otherHandler);

        public bool SourceLive => source is not GodotObject godot || GodotObject.IsInstanceValid(godot);

        public void Arm()
        {
            if (_armed || !SourceLive) { return; }
            _armed = true;
            subscribe();
        }

        public void Disarm()
        {
            if (!_armed) { return; }
            _armed = false;
            if (SourceLive) { unsubscribe(); }
        }
    }

    #endregion

    #region SEARCH_EXTENSIONS

    public static T GetFirstNodeOfTypeInScene<T>(bool includeScene = true, bool includeSubChildren = true)
        where T : Node
    {
        var scene = Engine.GetMainLoop() as SceneTree;
        if (scene == null)
        {
            throw new InvalidDataException(
                "ERROR || Scene is null, cannot get node!"); // why would scene tree be null? could be possible
        }

        if (scene.CurrentScene == null)
        {
            throw new InvalidDataException($"CurrentScene is null, cannot find {typeof(T).Name}!");
        }

        if (includeScene && scene.CurrentScene is T tScene)
        {
            return tScene;
        }

        return scene.CurrentScene.GetFirstChildOfType<T>(includeSubChildren);
    }

    public static bool TryGetNode<T>(this Node root, string nodePath, [MaybeNullWhen(false)] out T? result)
        where T : Node
    {
        result = null;
        if (root.HasNode(nodePath))
        {
            var node = root.GetNode(nodePath);
            if (node is T castedNode)
            {
                result = castedNode;
            }
        }

        return result != null;
    }

    public static bool TryGetFirstChildOfType<T>(this Node root, [MaybeNullWhen(false)] out T? result,
        bool includeSubChildren = false) where T : Node
    {
        if (!includeSubChildren)
        {
            Array<Node> children = root.GetChildren();
            foreach (var node in children)
            {
                if (node is T castedNode)
                {
                    result = castedNode;
                    return true;
                }
            }
        }
        else
        {
            var nodesToParse = new Queue<Node>(root.GetChildren());

            while (nodesToParse.Count > 0)
            {
                // Dequeue is an O(1) operation - very fast!
                var cursor = nodesToParse.Dequeue();
                if (cursor is T castedNode)
                {
                    result = castedNode;
                    return true;
                }

                // Enqueue each child to be processed later.
                foreach (var child in cursor.GetChildren())
                {
                    nodesToParse.Enqueue(child);
                }
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Finds a companion node for <paramref name="self" />: siblings first, then anywhere under the
    /// scene root it was authored into. For components that discover what they act on rather than
    /// taking an explicit <c>[Export]</c>.
    /// </summary>
    /// <remarks>
    /// A bare sibling search encodes a component's POSITION into what it can SEE — it works only
    /// while the component is a direct child of the entity root, and the moment a scene author groups
    /// nodes for organization the component's reach shrinks to that group and it silently finds
    /// nothing. Widening to the scene root makes placement a presentation concern.
    /// <para>
    /// Siblings still win, so a component that already resolved keeps resolving to the same node —
    /// this widens reach without re-targeting anything that previously worked. Past the sibling scope
    /// the walk is breadth-first from the scene root, so a tie between several of a type (multiple
    /// sprites, several colliders) resolves to whichever sits shallowest in authoring order — NOT to
    /// whichever is nearest this node. Where that distinction matters, wire an explicit
    /// <c>[Export]</c> instead of relying on discovery.
    /// </para>
    /// <para>
    /// <see cref="Node.Owner" /> is the scene root the node was authored into, and is null for the
    /// root itself and for nodes added at runtime — both fall back to the parent.
    /// </para>
    /// </remarks>
    public static bool TryGetFirstCompanionOfType<T>(this Node self, [MaybeNullWhen(false)] out T? result)
        where T : Node
    {
        var parent = self.GetParent();
        if (parent != null && parent.TryGetFirstChildOfType(out result))
        {
            return true;
        }

        var sceneRoot = self.Owner ?? parent;
        if (sceneRoot != null && sceneRoot.TryGetFirstChildOfType(out result, includeSubChildren: true))
        {
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Interface-typed counterpart of <see cref="TryGetFirstCompanionOfType{T}" />, for discovering a
    /// companion by capability rather than concrete type. Same scoping contract: siblings first, then
    /// anywhere under the scene root this node was authored into.
    /// </summary>
    public static bool TryGetFirstCompanionOfInterface<T>(this Node self, [MaybeNullWhen(false)] out T? result)
        where T : class
    {
        var parent = self.GetParent();
        if (parent != null && parent.TryGetFirstChildOfInterface(out result, includeSubChildren: false))
        {
            return true;
        }

        var sceneRoot = self.Owner ?? parent;
        if (sceneRoot != null && sceneRoot.TryGetFirstChildOfInterface(out result, includeSubChildren: true))
        {
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryGetFirstChildOfInterface<T>(this Node root, [MaybeNullWhen(false)] out T? result,
        bool includeSubChildren = true) where T : class
    {
        result = null;
        if (!includeSubChildren)
        {
            Array<Node> children = root.GetChildren();
            foreach (var node in children)
            {
                if (node is T castedNode)
                {
                    result = castedNode;
                    return true;
                }
            }
        }
        else
        {
            var nodesToParse = new Queue<Node>(root.GetChildren());
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse.Dequeue();
                if (cursor is T castedNode)
                {
                    result = castedNode;
                    return true;
                }

                foreach (var child in cursor.GetChildren())
                {
                    nodesToParse.Enqueue(child);
                }
            }
        }

        return false;
    }

    public static bool TryGetChildOfInterface<T>(this Node root, string nodePath, [MaybeNullWhen(false)] out T? result)
        where T : class
    {
        result = null;
        if (root.HasNode(nodePath))
        {
            var node = root.GetNode(nodePath);
            if (node is T castedNode)
            {
                result = castedNode;
            }
        }

        return result != null;
    }

    public static T GetFirstChildOfType<T>(this Node root, bool includeSubChildren = true) where T : Node
    {
        if (root.TryGetFirstChildOfType(out T? result, includeSubChildren))
        {
            return result!;
        }

        throw new InvalidDataException($"Couldn't find a child of type {typeof(T).Name} in node {root.Name}");
    }

    public static T GetFirstChildOfInterface<T>(this Node root, bool includeSubChildren = true) where T : class
    {
        if (root.TryGetFirstChildOfInterface(out T? result, includeSubChildren))
        {
            return result!;
        }

        throw new InvalidDataException($"Couldn't find a child of interface {typeof(T).Name} in node {root.Name}");
    }

    /// <summary>
    /// Walks the parent chain from <paramref name="start"/> upward and returns the first
    /// ancestor matching <typeparamref name="T"/>. Returns <c>null</c> if none found.
    /// Set <paramref name="includeSelf"/> to <c>true</c> to also consider <paramref name="start"/>.
    /// </summary>
    public static T? FindFirstAncestorOfType<T>(this Node start, bool includeSelf = false) where T : class
    {
        var cursor = includeSelf ? (Node?)start : start.GetParent();
        while (cursor != null)
        {
            if (cursor is T match)
            {
                return match;
            }
            cursor = cursor.GetParent();
        }
        return null;
    }

    public static Array<T> GetChildrenOfType<[MustBeVariant] T>(this Node root, bool includeSubChildren = true)
        where T : Node
    {
        var childArray = new Array<T>();
        if (!includeSubChildren)
        {
            foreach (var node in root.GetChildren())
            {
                if (node is T castNode)
                {
                    childArray.Add(castNode);
                }
            }
        }
        else
        {
            var nodesToParse = new Queue<Node>(root.GetChildren());
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse.Dequeue();
                if (cursor is T castedNode)
                {
                    childArray.Add(castedNode);
                }

                foreach (var child in cursor.GetChildren())
                {
                    nodesToParse.Enqueue(child);
                }
            }
        }

        return childArray;
    }

    public static IEnumerable<T> GetChildrenOfInterface<T>(this Node root, bool includeSubChildren = true) where T : class
    {
        var childArray = new List<T>();
        if (!includeSubChildren)
        {
            foreach (var node in root.GetChildren())
            {
                if (node is T castNode)
                {
                    childArray.Add(castNode);
                }
            }
        }
        else
        {
            var nodesToParse = new Queue<Node>(root.GetChildren());
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse.Dequeue();
                if (cursor is T castedNode)
                {
                    childArray.Add(castedNode);
                }

                foreach (var child in cursor.GetChildren())
                {
                    nodesToParse.Enqueue(child);
                }
            }
        }

        return childArray;
    }

    public static Array<Node> GetAllChildrenNodesInGroup(this Node root, StringName groupName,
        bool includeSubChildren = true)
    {
        Array<Node> children = root.GetChildren();
        var groupChildren = new Array<Node>();

        if (!includeSubChildren)
        {
            foreach (var child in children)
            {
                if (child.IsInGroup(groupName))
                {
                    groupChildren.Add(child);
                }
            }
        }
        else
        {
            var nodesToParse = new Queue<Node>(root.GetChildren());
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse.Dequeue();
                if (cursor.IsInGroup(groupName))
                {
                    groupChildren.Add(cursor);
                }

                foreach (var child in cursor.GetChildren())
                {
                    nodesToParse.Enqueue(child);
                }
            }
        }

        return groupChildren;
    }

    public static Array<T> GetAllNodesOfTypeInScene<[MustBeVariant] T>(bool includeSubChildren = true) where T : Node
    {
        var currentScene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
        return currentScene == null ? new Array<T>() : currentScene.GetChildrenOfType<T>(includeSubChildren);
    }

    public static IEnumerable<T> GetAllNodesOfInterfaceInScene<T>(bool includeSubChildren = true) where T : class
    {
        var currentScene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
        return currentScene == null ? new List<T>() : currentScene.GetChildrenOfInterface<T>(includeSubChildren);
    }

    #endregion

    #region IDENTITY_EXTENSIONS

    /// <summary>
    /// Walks <paramref name="node"/> and its ancestors (upward, starting at
    /// <paramref name="node"/> itself) for the first <see cref="IIdentifiable"/>. Returns the
    /// matching provider, or <c>null</c> when none is found.
    /// </summary>
    /// <remarks>
    /// This is the promoted form of the hit path's private identity walk — an UPWARD traversal.
    /// It is NOT the same traversal as the downward child-walks
    /// (<c>TryGetFirstChildOfInterface&lt;IIdentifiable&gt;</c>); the two must not be conflated.
    /// </remarks>
    public static IIdentifiable? TryResolveIdentifiable(this Node? node)
    {
        var current = node;
        while (current != null)
        {
            if (current is IIdentifiable identifiable) { return identifiable; }
            current = current.GetParent();
        }
        return null;
    }

    /// <summary>
    /// True if the node carries an <see cref="IIdentifiable"/> child whose identity
    /// includes <paramref name="category"/>. Walks the node's children to find the
    /// identity provider; returns false if no provider or category is null.
    /// </summary>
    /// <remarks>
    /// Centralizes the canonical
    /// <c>TryGetFirstChildOfInterface&lt;IIdentifiable&gt; → GetIdentity().HasCategory()</c>
    /// chain so AI consumers asking "is this collider an entity / spell / hazard?" share
    /// one implementation. Cache the result if calling per-frame for the same node.
    /// </remarks>
    public static bool HasCategory(this Node node, Category? category)
    {
        if (category == null)
        {
            return false;
        }

        if (!node.TryGetFirstChildOfInterface<IIdentifiable>(out var identifiable))
        {
            return false;
        }

        return identifiable!.GetIdentity().HasCategory(category);
    }

    #endregion
}
