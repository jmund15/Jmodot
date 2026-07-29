namespace Jmodot.Implementation.Shared;

using Core.AI.BB;

/// <summary>
/// Editor-time dependency checks shared by every component that hard-fails on a missing
/// entity-scoped sibling. Centralised so the search topology matches what the entity
/// initializer actually does.
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
        for (var ancestor = self.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor.TryGetFirstChildOfType<T>(out _))
            {
                return null;
            }

            if (ancestor.TryGetFirstChildOfInterface<IBlackboard>(out _, includeSubChildren: false))
            {
                break;
            }
        }

        return warning;
    }
}
