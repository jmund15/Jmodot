namespace Jmodot.Implementation.Shared;

using System.Collections.Generic;
using Core.AI.BB;
using Core.Movement.Quirks;
using Core.Shared;
using GColl = Godot.Collections;

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
            if (ancestor.TryGetFirstChildOfType<T>(out found, includeSubChildren: true))
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
    /// Renders the configuration faults of an authored movement-quirk slot list in the
    /// <see cref="ResourceSlotWarnings{T}"/> format, labelled "Movement Quirks".
    /// </summary>
    public static string[] QuirkSlotWarnings(GColl.Array<MovementQuirk3D>? quirks)
        => ResourceSlotWarnings("Movement Quirks", quirks);

    /// <summary>
    /// Renders the configuration faults of an authored Resource slot list: one line per fault,
    /// each prefixed with <paramref name="slotLabel"/>, the slot index and the resource's name and
    /// type, so the Inspector shows one bullet per fault instead of a joined blob. A null slot is
    /// its own warning naming the index and no resource. Only resources implementing
    /// <see cref="IResourceConfigurationWarnings"/> contribute faults. A legal empty list yields none.
    /// </summary>
    public static string[] ResourceSlotWarnings<T>(string slotLabel, IList<T>? slots) where T : Resource
    {
        if (slots == null || slots.Count == 0) { return System.Array.Empty<string>(); }

        var warnings = new List<string>();
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null)
            {
                warnings.Add($"{slotLabel} slot {i}: empty slot — nothing is assigned here.");
                continue;
            }

            if (slot is not IResourceConfigurationWarnings source) { continue; }

            var type = slot.GetType().Name;
            var name = string.IsNullOrEmpty(slot.ResourceName) ? type : slot.ResourceName;
            foreach (var warning in source.GetResourceConfigurationWarnings())
            {
                warnings.Add($"{slotLabel} slot {i} '{name}' ({type}): {warning}");
            }
        }
        return warnings.ToArray();
    }
}
