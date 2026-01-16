namespace Jmodot.Core.Combat;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Registry that stores and looks up Category interactions.
/// Used to determine what happens when effects with different elemental
/// categories interact (e.g., Water cancels Fire).
/// </summary>
[GlobalClass]
public partial class CategoryInteractionRegistry : Resource
{
    /// <summary>
    /// All registered interactions.
    /// </summary>
    [Export] public Godot.Collections.Array<CategoryInteraction> Interactions { get; private set; } = new();

    /// <summary>
    /// Finds the interaction rule for the given incoming and existing identities.
    /// </summary>
    /// <param name="incoming">The identity of the effect being applied.</param>
    /// <param name="existing">The identity of the active effect.</param>
    /// <returns>The matching interaction, or null if none found.</returns>
    public CategoryInteraction? GetInteraction(Identity? incoming, Identity? existing)
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
    /// Gets all interactions that apply when the given identity is incoming.
    /// Includes bidirectional interactions where this identity could be "existing".
    /// </summary>
    /// <param name="incoming">The identity of the effect being applied.</param>
    /// <returns>All matching interactions.</returns>
    public IEnumerable<CategoryInteraction> GetInteractionsForIncoming(Identity? incoming)
    {
        if (incoming == null)
        {
            yield break;
        }

        foreach (var interaction in Interactions)
        {
            if (interaction.IncomingCategory == incoming)
            {
                yield return interaction;
            }
            else if (interaction.IsBidirectional && interaction.ExistingCategory == incoming)
            {
                yield return interaction;
            }
        }
    }

    /// <summary>
    /// Adds an interaction to the registry.
    /// </summary>
    /// <param name="interaction">The interaction to add.</param>
    public void AddInteraction(CategoryInteraction interaction)
    {
        Interactions.Add(interaction);
    }
}
