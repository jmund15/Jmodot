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
        var type = resource.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var prop in type.GetProperties(flags))
        {
            if (prop.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            var value = prop.GetValue(resource);
            if (value == null)
            {
                throw new ResourceConfigurationException(
                    $"Required export '{prop.Name}' must be assigned for resource {resource.ResourceName}.", resource);
            }
        }

        foreach (var field in type.GetFields(flags))
        {
            if (field.GetCustomAttribute<RequiredExportAttribute>() == null)
            {
                continue;
            }

            var value = field.GetValue(resource);
            if (value == null)
            {
                throw new ResourceConfigurationException(
                    $"Required export '{field.Name}' must be assigned for resource {resource.ResourceName}.", resource);
            }
        }
    }
}
