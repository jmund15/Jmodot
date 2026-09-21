// Intentionally in the global namespace for extension method discoverability.
// Multiple files use these extensions without explicit imports. Do not add a namespace declaration.

using System.Collections.Generic;
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
        ValidateRequiredExports(resource, new HashSet<Resource>());
    }

    /// <param name="visited">
    /// Resources already being validated on this walk. A `.tres` graph may hold a cycle (A requires B
    /// requires A), which recursion alone turns into a StackOverflow — uncatchable, so it takes the
    /// process down rather than reporting a configuration error.
    /// </param>
    private static void ValidateRequiredExports(Resource resource, HashSet<Resource> visited)
    {
        if (!visited.Add(resource))
        {
            return;
        }

        var type = resource.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            ValidateRequiredExportMember(prop.GetValue(resource), prop.Name, resource, visited);
        }

        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            ValidateRequiredExportMember(field.GetValue(resource), field.Name, resource, visited);
        }
    }

    /// <summary>
    /// Validates a single [RequiredExport] member's value: fails if null; recurses into a nested
    /// Resource's own [RequiredExport] members; for an enumerable (Godot.Collections.Array/typed
    /// array), fails on any null element (naming its index) and recurses into Resource elements.
    /// Recursion follows only members/elements the schema itself marks [RequiredExport] — an
    /// assigned Resource sitting behind a plain, non-required member is never walked.
    /// </summary>
    private static void ValidateRequiredExportMember(object? value, string memberName, Resource owner, HashSet<Resource> visited)
    {
        if (value == null)
        {
            throw new ResourceConfigurationException(
                $"Required export '{memberName}' must be assigned for resource {owner.ResourceName}.", owner);
        }

        if (value is Resource nestedResource)
        {
            ValidateRequiredExports(nestedResource, visited);
            return;
        }

        if (value is string || value is not System.Collections.IEnumerable enumerable)
        {
            return;
        }

        var index = 0;
        foreach (var element in enumerable)
        {
            if (element == null)
            {
                throw new ResourceConfigurationException(
                    $"Required export '{memberName}[{index}]' must be assigned for resource {owner.ResourceName}.", owner);
            }

            if (element is Resource nestedElement)
            {
                ValidateRequiredExports(nestedElement, visited);
            }

            index++;
        }
    }
}
