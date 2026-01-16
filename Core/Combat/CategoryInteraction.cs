namespace Jmodot.Core.Combat;

using Godot;
using Jmodot.Core.Identification;

/// <summary>
/// Defines an interaction rule between two Identities (typically Labels).
/// When an effect with IncomingCategory is applied to an entity
/// that has an active effect with ExistingCategory, this interaction triggers.
/// </summary>
[GlobalClass]
public partial class CategoryInteraction : Resource
{
    /// <summary>
    /// The Identity (Label) of the effect being applied.
    /// </summary>
    [Export] public Identity? IncomingCategory { get; private set; }

    /// <summary>
    /// The Identity (Label) of the already-active effect.
    /// </summary>
    [Export] public Identity? ExistingCategory { get; private set; }

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
    /// Checks if this interaction matches the given incoming and existing identities.
    /// </summary>
    /// <param name="incoming">The identity of the effect being applied.</param>
    /// <param name="existing">The identity of the active effect.</param>
    /// <returns>True if this interaction applies.</returns>
    public bool Matches(Identity? incoming, Identity? existing)
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
    internal void SetIncomingCategory(Identity? value) => IncomingCategory = value;

    /// <summary>Sets ExistingCategory for testing purposes.</summary>
    internal void SetExistingCategory(Identity? value) => ExistingCategory = value;

    /// <summary>Sets Effect for testing purposes.</summary>
    internal void SetEffect(CategoryInteractionEffect value) => Effect = value;

    /// <summary>Sets Magnitude for testing purposes.</summary>
    internal void SetMagnitude(float value) => Magnitude = value;

    /// <summary>Sets IsBidirectional for testing purposes.</summary>
    internal void SetIsBidirectional(bool value) => IsBidirectional = value;

    #endregion
}
