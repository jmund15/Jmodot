namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;
using System.Reflection;
using Core.AI.BB;
using Core.Shared.Attributes;

/// <summary>
/// Editor-time configuration checks for the dock (<c>_GetConfigurationWarnings</c>), each sharing
/// its topology with the runtime check it previews, so the dock and the runtime failure agree.
/// </summary>
public static class ConfigWarnings
{
    /// <summary>
    /// Returns <paramref name="warning"/> when no node of type <typeparamref name="T"/> lives
    /// under the entity that owns <paramref name="self"/>, otherwise null.
    /// </summary>
    /// <remarks>
    /// The entity initializer resolves components through a full descendant walk from the entity
    /// root, so a bare <c>GetParent()</c> check false-warns on any component nested a level
    /// deeper than the root. This climbs ancestors — scanning each one's descendants — and stops
    /// at the first ancestor owning a blackboard child, which is the entity root the initializer
    /// runs over.
    /// </remarks>
    public static string? RequireEntitySibling<T>(Node self, string warning) where T : Node
    {
        return TryFindEntitySibling<T>(self, out _) ? null : warning;
    }

    /// <summary>
    /// Finds the first node of type <typeparamref name="T"/> under the entity that owns
    /// <paramref name="self"/>, using the same ancestor walk
    /// <see cref="RequireEntitySibling{T}"/> performs. Editor-time only, and first-match: on an
    /// entity carrying two candidates the runtime's blackboard-resolved instance and this one can
    /// differ, which is already a scene-authoring defect the entity initializer warns about.
    /// </summary>
    public static bool TryFindEntitySibling<T>(Node self, out T? found) where T : Node
    {
        for (var ancestor = self.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor.TryGetFirstChildOfType<T>(out found))
            {
                return true;
            }

            if (ancestor.TryGetFirstChildOfInterface<IBlackboard>(out _, includeSubChildren: false))
            {
                break;
            }
        }

        found = null;
        return false;
    }

    /// <summary>
    /// One ready-formatted dock warning per unassigned <see cref="RequiredExportAttribute"/> member of
    /// <paramref name="obj"/>, naming its Inspector name and, when authored, the attribute's
    /// <see cref="RequiredExportAttribute.Consequence"/>. Empty when every required member is set.
    /// Append it from <c>_GetConfigurationWarnings</c>; it reports the same members
    /// <c>ValidateRequiredExports</c> throws for at runtime.
    /// </summary>
    public static string[] RequiredExports(GodotObject obj)
    {
        var warnings = new List<string>();
        foreach (var missing in UnassignedRequiredExports(obj))
        {
            var warning = $"Required export '{missing.MemberName.Capitalize()}' is unassigned.";
            warnings.Add(missing.Consequence == null ? warning : $"{warning} {missing.Consequence}");
        }

        return warnings.ToArray();
    }

    internal readonly record struct UnassignedRequiredExport(string MemberName, string? Consequence);

    /// <summary>
    /// The one reflection walk behind <see cref="RequiredExports"/> and both
    /// <c>ValidateRequiredExports</c> extensions: properties then fields, most-derived type first,
    /// reading each level's declared members so private members of base classes are included.
    /// </summary>
    internal static IEnumerable<UnassignedRequiredExport> UnassignedRequiredExports(GodotObject obj)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                   | BindingFlags.DeclaredOnly;
        var engineAssembly = typeof(GodotObject).Assembly;
        var seenProperties = new HashSet<string>();

        for (var type = obj.GetType(); type != null && type.Assembly != engineAssembly; type = type.BaseType)
        {
            foreach (var prop in type.GetProperties(flags))
            {
                var attribute = prop.GetCustomAttribute<RequiredExportAttribute>(inherit: false);
                if (attribute == null || !seenProperties.Add(prop.Name) || prop.GetValue(obj) != null)
                {
                    continue;
                }

                yield return new UnassignedRequiredExport(prop.Name, attribute.Consequence);
            }
        }

        for (var type = obj.GetType(); type != null && type.Assembly != engineAssembly; type = type.BaseType)
        {
            foreach (var field in type.GetFields(flags))
            {
                var attribute = field.GetCustomAttribute<RequiredExportAttribute>(inherit: false);
                if (attribute == null || field.GetValue(obj) != null)
                {
                    continue;
                }

                yield return new UnassignedRequiredExport(field.Name, attribute.Consequence);
            }
        }
    }
}
