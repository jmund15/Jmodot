#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Godot.Collections;
using Jmodot.Core.Shared.Attributes;
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

    public static void SafeQueueFree(this Node node)
    {
        if (node.IsValid())
        {
            node.QueueFree();
        }
        else
        {
            GD.PrintErr("Couldn't Safely Queue Free node: ", node.Name, ", node is not valid");
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
        var type = node.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Check properties
        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            var value = prop.GetValue(node);
            if (value == null)
            {
                throw new NodeConfigurationException(
                    $"Required export '{prop.Name}' must be assigned in the Inspector.", node);
            }
        }

        // Check fields
        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            var value = field.GetValue(node);
            if (value == null)
            {
                throw new NodeConfigurationException(
                    $"Required export '{field.Name}' must be assigned in the Inspector.", node);
            }
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

        if (includeScene && scene.CurrentScene is T tScene)
        {
            return tScene;
        }

        return scene.CurrentScene.GetFirstChildOfType<T>(includeSubChildren);
    }

    //public static T? GetFirstChildOfTypeOldWArray<T>(this Node root, bool includeSubChildren = true) where T : Node
    //{
    //    if (!includeSubChildren)
    //    {
    //        Array<Node> children = root.GetChildren();
    //        foreach (var node in children)
    //        {
    //            if (node is T castedNode)
    //                return castedNode;
    //        }
    //    }
    //    else
    //    {
    //        Array<Node> nodesToParse = root.GetChildren();
    //        while (nodesToParse.Count > 0)
    //        {
    //            var cursor = nodesToParse[0];
    //            nodesToParse.Remove(cursor);
    //            if (cursor is T castedNode)
    //                return castedNode;
    //            nodesToParse.AddRange(cursor.GetChildren());
    //        }
    //    }
    //    return null;
    //}
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
            //Array<Node> nodesToParse = root.GetChildren();
            var nodesToParse = new Queue<Node>(root.GetChildren());
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse.Dequeue();
                //nodesToParse.Remove(cursor);
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
            Array<Node> nodesToParse = root.GetChildren();
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse[0];
                nodesToParse.Remove(cursor);
                if (cursor is T castedNode)
                {
                    childArray.Add(castedNode);
                }

                nodesToParse.AddRange(cursor.GetChildren());
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
            Array<Node> nodesToParse = root.GetChildren();
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse[0];
                nodesToParse.Remove(cursor);
                if (cursor is T castedNode)
                {
                    childArray.Add(castedNode);
                }

                nodesToParse.AddRange(cursor.GetChildren());
            }
        }

        return childArray;
    }
    // SAME AS "GetChildrenOfType" once we added the "includeSubChildren" param, so it's redundant
    //public static Array<T> GetAllNodesOfType<[MustBeVariant] T>(this Node root) where T : Node
    //{
    //    Array<Node> nodesToParse = root.GetChildren();
    //    Array<T> castedNodes = new Array<T>();
    //    while (nodesToParse.Count > 0)
    //    {
    //        var cursor = nodesToParse[0];
    //        nodesToParse.Remove(cursor);
    //        if (cursor is T castedNode)
    //            castedNodes.Add(castedNode);
    //        nodesToParse.AddRange(cursor.GetChildren());
    //    }

    //    return castedNodes;
    //}
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
            Array<Node> nodesToParse = root.GetChildren();
            while (nodesToParse.Count > 0)
            {
                var cursor = nodesToParse[0];
                nodesToParse.Remove(cursor);
                if (cursor.IsInGroup(groupName))
                {
                    groupChildren.Add(cursor);
                }

                nodesToParse.AddRange(cursor.GetChildren());
            }
        }

        return groupChildren;
    }

    public static Array<T> GetAllNodesOfTypeInScene<[MustBeVariant] T>(bool includeSubChildren = true) where T : Node
    {
        var currentScene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
        return currentScene == null ? new Array<T>() : currentScene.GetChildrenOfType<T>(includeSubChildren);
        //includeSubChildren ? currentScene.GetAllNodesOfType<T>() : currentScene.GetChildrenOfType<T>();
    }

    public static IEnumerable<T> GetAllNodesOfInterfaceInScene<T>(bool includeSubChildren = true) where T : class
    {
        var currentScene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
        return currentScene == null ? new List<T>() : currentScene.GetChildrenOfInterface<T>(includeSubChildren);
        //includeSubChildren ? currentScene.GetAllNodesOfType<T>() : currentScene.GetChildrenOfType<T>();
    }

    #endregion
}
