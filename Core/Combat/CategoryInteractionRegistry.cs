namespace Jmodot.Core.Combat;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Registry that stores and looks up Category interactions.
/// Used to determine what happens when effects with different categories
/// interact (e.g., Water cancels Fire). Lookups use hierarchical matching.
/// </summary>
[GlobalClass]
public partial class CategoryInteractionRegistry : Resource
{
    /// <summary>
    /// All registered interactions.
    /// </summary>
    [Export] public Godot.Collections.Array<CategoryInteraction> Interactions { get; private set; } = new();

    /// <summary>
    /// Finds the interaction rule for the given incoming and existing categories.
    /// </summary>
    public CategoryInteraction? GetInteraction(Category? incoming, Category? existing)
    {
        if (incoming == null || existing == null)
        {
            return null;
        }

        foreach (var interaction in Interactions)
        {
            if (interaction.Matches(incoming, existing))
            {
                return interaction;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all interactions that apply when the given category is incoming.
    /// Includes bidirectional interactions where this category could be "existing".
    /// Uses hierarchical matching against IncomingCategory/ExistingCategory.
    /// </summary>
    public IEnumerable<CategoryInteraction> GetInteractionsForIncoming(Category? incoming)
    {
        if (incoming == null)
        {
            yield break;
        }

        foreach (var interaction in Interactions)
        {
            if (incoming.IsOrDescendsFrom(interaction.IncomingCategory))
            {
                yield return interaction;
            }
            else if (interaction.IsBidirectional && incoming.IsOrDescendsFrom(interaction.ExistingCategory))
            {
                yield return interaction;
            }
        }
    }

    /// <summary>
    /// Adds an interaction to the registry.
    /// </summary>
    public void AddInteraction(CategoryInteraction interaction)
    {
        Interactions.Add(interaction);
    }
}
