namespace Jmodot.Core.Combat;

using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Defines an interaction rule between two Categories.
/// When an effect with IncomingCategory is applied to an entity
/// that has an active effect with ExistingCategory, this interaction triggers.
/// </summary>
[GlobalClass]
public partial class CategoryInteraction : Resource
{
    /// <summary>
    /// The Category of the effect being applied.
    /// </summary>
    [Export] public Category? IncomingCategory { get; private set; }

    /// <summary>
    /// The Category of the already-active effect.
    /// </summary>
    [Export] public Category? ExistingCategory { get; private set; }

    /// <summary>
    /// What happens when these categories interact.
    /// </summary>
    [Export] public CategoryInteractionEffect Effect { get; private set; } = CategoryInteractionEffect.CancelExisting;

    /// <summary>
    /// Magnitude for duration-based effects (ReduceDuration, Amplify).
    /// </summary>
    [Export] public float Magnitude { get; private set; } = 0f;

    /// <summary>
    /// If true, the interaction works in both directions
    /// (A->B triggers same as B->A).
    /// </summary>
    [Export] public bool IsBidirectional { get; private set; } = false;

    /// <summary>
    /// Checks if this interaction matches the given incoming and existing categories.
    /// </summary>
    /// <param name="incoming">The category of the effect being applied.</param>
    /// <param name="existing">The category of the active effect.</param>
    /// <returns>True if this interaction applies.</returns>
    public bool Matches(Category? incoming, Category? existing)
    {
        if (incoming == null || existing == null)
        {
            return false;
        }

        if (IncomingCategory == null || ExistingCategory == null)
        {
            return false;
        }

        // Direct match
        if (IncomingCategory == incoming && ExistingCategory == existing)
        {
            return true;
        }

        // Bidirectional reverse match
        if (IsBidirectional && IncomingCategory == existing && ExistingCategory == incoming)
        {
            return true;
        }

        return false;
    }

    #region Test Helpers

    /// <summary>Sets IncomingCategory for testing purposes.</summary>
    internal void SetIncomingCategory(Category? value) => IncomingCategory = value;

    /// <summary>Sets ExistingCategory for testing purposes.</summary>
    internal void SetExistingCategory(Category? value) => ExistingCategory = value;

    /// <summary>Sets Effect for testing purposes.</summary>
    internal void SetEffect(CategoryInteractionEffect value) => Effect = value;

    /// <summary>Sets Magnitude for testing purposes.</summary>
    internal void SetMagnitude(float value) => Magnitude = value;

    /// <summary>Sets IsBidirectional for testing purposes.</summary>
    internal void SetIsBidirectional(bool value) => IsBidirectional = value;

    #endregion
}
