// Intentionally in the global namespace for extension method discoverability.
// Multiple files use these extensions without explicit imports. Do not add a namespace declaration.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared.GodotExceptions;

public static class ResourceExts
{
    /// <summary>
    /// Validates that all properties and fields marked with [RequiredExport] are not null.
    /// Call this during Resource initialization to fail-fast with a clear error if any required exports are missing.
    /// </summary>
    /// <exception cref="ResourceConfigurationException">
    /// Thrown when a [RequiredExport] property or field is null.
    /// </exception>
    /// <example>
    /// <code>
    /// [Export, RequiredExport] public Resource Config { get; set; } = null!;
    /// </code>
    /// </example>
    public static void ValidateRequiredExports(this Resource resource)
    {
        var missing = resource.FindMissingRequiredExports();
        if (missing.Count > 0)
        {
            throw new ResourceConfigurationException(
                $"Required export '{missing[0]}' must be assigned for resource {resource.ResourceName}.", resource);
        }
    }

    /// <summary>
    /// Names of every [RequiredExport] property and field on <paramref name="resource"/> whose value is
    /// null, in declaration-reflection order (properties, then fields). Never throws; empty means every
    /// required export is assigned. The non-throwing twin of <see cref="ValidateRequiredExports"/>, for
    /// Resources that report defects as messages.
    /// </summary>
    public static IReadOnlyList<string> FindMissingRequiredExports(this Resource resource)
    {
        var type = resource.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var missing = new List<string>();

        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetCustomAttribute<RequiredExportAttribute>() != null && prop.GetValue(resource) == null)
            {
                missing.Add(prop.Name);
            }
        }

        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<RequiredExportAttribute>() != null && field.GetValue(resource) == null)
            {
                missing.Add(field.Name);
            }
        }

        return missing;
    }

    /// <summary>
    /// Messages for every public <c>[Export]</c> number on <paramref name="resource"/> the Inspector could not
    /// have produced: a non-finite <see cref="float"/> or <see cref="double"/>, one outside its
    /// <see cref="PropertyHint.Range"/> (unless the hint's <c>or_greater</c> / <c>or_less</c> flag allows that
    /// side), or a <see cref="Color"/> with a non-finite channel. Never throws; empty means every numeric
    /// export is finite and in range.
    /// </summary>
    public static IReadOnlyList<string> FindOutOfRangeExports(this Resource resource)
    {
        var messages = new List<string>();
        foreach (var property in resource.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var export = property.GetCustomAttribute<ExportAttribute>();
            if (export is null || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var value = property.GetValue(resource);
            var message = value switch
            {
                float f => OutOfRangeMessage(property.Name, f, f.ToString(CultureInfo.InvariantCulture), export, singlePrecision: true),
                double d => OutOfRangeMessage(property.Name, d, d.ToString(CultureInfo.InvariantCulture), export, singlePrecision: false),
                Color c => NonFiniteColorMessage(property.Name, c),
                _ => null,
            };
            if (message != null)
            {
                messages.Add(message);
            }
        }

        return messages;
    }

    /// <summary>
    /// Stores <paramref name="incoming"/> in <paramref name="slot"/> and moves the forwarding of the slot's
    /// <c>changed</c> signal to it. The outgoing value is disconnected from <paramref name="forward"/> only when
    /// none of <paramref name="otherSlots"/> still holds it; <paramref name="incoming"/> is connected once, so a
    /// re-run with the same value (a setter re-run after an editor C# reload restores exports) never
    /// double-connects. Returns false and touches nothing when the slot already holds <paramref name="incoming"/>.
    /// </summary>
    /// <remarks>Owning the store removes the store-then-rewire ordering a separate rewire call would require.</remarks>
    /// <param name="forward">An ObjectID-bound callable, <c>new Callable(owner, MethodName.X)</c>. The engine
    /// revalidates its target before every dispatch; a delegate-backed callable it cannot, so one is rejected.</param>
    /// <param name="otherSlots">Every other slot on the owner that forwards through <paramref name="forward"/>.</param>
    /// <returns>True when the slot's value changed.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="forward"/> has no GodotObject target or no method name.</exception>
    /// <exception cref="System.InvalidOperationException">The engine refused to connect <paramref name="incoming"/>.</exception>
    public static bool SetForwardedSlot<T>(ref T slot, T incoming, Callable forward, params Resource?[] otherSlots)
        where T : Resource?
    {
        if (forward.Target is null || forward.Method is null || forward.Method.IsEmpty)
        {
            throw new System.ArgumentException(
                "SetForwardedSlot needs an ObjectID-bound callable (new Callable(owner, MethodName.X)); a delegate-backed callable cannot be revalidated by the engine.",
                nameof(forward));
        }

        if (ReferenceEquals(slot, incoming))
        {
            return false;
        }

        var outgoing = slot;
        Rewire(forward, outgoing, incoming, otherSlots);
        slot = incoming;
        return true;
    }

    /// <summary>
    /// A label for <paramref name="resource"/> in messages that is never empty: its path, else its name,
    /// else its type name.
    /// </summary>
    public static string DisplayLabel(this Resource resource)
    {
        if (!string.IsNullOrEmpty(resource.ResourcePath)) { return resource.ResourcePath; }
        if (!string.IsNullOrEmpty(resource.ResourceName)) { return resource.ResourceName; }
        return resource.GetType().Name;
    }

    /// <summary>
    /// The ClassDB class of <paramref name="scene"/>'s root node, following an inherited scene through
    /// <see cref="SceneState.GetBaseSceneState"/> to the base that names it; null when no state in the chain names one.
    /// </summary>
    public static string? FindRootNativeClass(this PackedScene scene)
    {
        for (var state = scene.GetState(); state != null; state = state.GetBaseSceneState())
        {
            // GetNodeType(0) on a state with no nodes logs an engine index ERROR.
            if (state.GetNodeCount() == 0) { return null; }

            var rootType = state.GetNodeType(0).ToString();
            if (!string.IsNullOrEmpty(rootType)) { return rootType; }
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="scene"/>'s root class (<see cref="FindRootNativeClass"/>) is or inherits
    /// <paramref name="nativeClass"/>, a native ClassDB name; false when no state in the chain names a root.
    /// </summary>
    public static bool RootInherits(this PackedScene scene, string nativeClass)
    {
        var rootType = scene.FindRootNativeClass();
        return rootType != null && (rootType == nativeClass || ClassDB.IsParentClass(rootType, nativeClass));
    }

    private static string? OutOfRangeMessage(string name, double value, string shown, ExportAttribute export, bool singlePrecision)
    {
        if (!double.IsFinite(value))
        {
            return $"'{name}' is {shown}; enter a finite number.";
        }

        if (export.Hint != PropertyHint.Range)
        {
            return null;
        }

        var fields = export.HintString.Split(',').Select(field => field.Trim()).ToArray();
        if (fields.Length < 2
            || !double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var min)
            || !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
        {
            return null;
        }

        // A float export compares against float-rounded bounds: 0.3f widens to 0.30000001, above a parsed 0.3.
        if (singlePrecision)
        {
            min = (float)min;
            max = (float)max;
        }

        var belowAllowed = fields.Contains("or_less");
        var aboveAllowed = fields.Contains("or_greater");
        if ((value < min && !belowAllowed) || (value > max && !aboveAllowed))
        {
            return $"'{name}' is {shown}, outside its Inspector range {fields[0]} to {fields[1]}; re-enter it in the Inspector.";
        }

        return null;
    }

    private static string? NonFiniteColorMessage(string name, Color color)
    {
        if (float.IsFinite(color.R) && float.IsFinite(color.G) && float.IsFinite(color.B) && float.IsFinite(color.A))
        {
            return null;
        }

        return $"'{name}' has a non-finite channel ({color}); pick the color again.";
    }

    private static void Rewire(Callable forward, Resource? outgoing, Resource? incoming, Resource?[]? otherSlots)
    {
        if (incoming != null && !incoming.IsConnected(Resource.SignalName.Changed, forward))
        {
            var connected = incoming.Connect(Resource.SignalName.Changed, forward);
            if (connected != Error.Ok)
            {
                throw new System.InvalidOperationException(
                    $"SetForwardedSlot could not connect {incoming.DisplayLabel()} changed to {forward.Method}: {connected}.");
            }
        }

        if (outgoing != null && !IsHeld(outgoing, otherSlots) && outgoing.IsConnected(Resource.SignalName.Changed, forward))
        {
            outgoing.Disconnect(Resource.SignalName.Changed, forward);
        }
    }

    private static bool IsHeld(Resource resource, Resource?[]? slots)
    {
        if (slots == null)
        {
            return false;
        }

        foreach (var slot in slots)
        {
            if (ReferenceEquals(slot, resource))
            {
                return true;
            }
        }

        return false;
    }
}
