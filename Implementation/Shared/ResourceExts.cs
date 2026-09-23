// Intentionally in the global namespace for extension method discoverability.
// Multiple files use these extensions without explicit imports. Do not add a namespace declaration.

using Godot;
using Jmodot.Implementation.Shared;
using Jmodot.Implementation.Shared.GodotExceptions;

public static class ResourceExts
{
    /// <summary>
    /// Validates that all properties and fields marked with [RequiredExport] are not null, including
    /// members declared on base classes (private ones too).
    /// Call this during Resource initialization to fail-fast with a clear error if any required exports are missing.
    /// </summary>
    /// <exception cref="ResourceConfigurationException">
    /// Thrown for the first [RequiredExport] property or field that is null.
    /// </exception>
    /// <example>
    /// <code>
    /// [Export, RequiredExport] public Resource Config { get; set; } = null!;
    /// </code>
    /// </example>
    public static void ValidateRequiredExports(this Resource resource)
    {
        foreach (var missing in ConfigWarnings.UnassignedRequiredExports(resource))
        {
            throw new ResourceConfigurationException(
                $"Required export '{missing.MemberName}' must be assigned for resource {resource.ResourceName}.", resource);
        }
    }
}
