namespace Jmodot.Core.Identification;

using Implementation.AI.Perception.Strategies;

/// <summary>
///     A data-driven Resource representing a high-level, abstract category or "tag".
///     This is a cornerstone of the world's semantic system, allowing for broad-level grouping and querying.
///     For example, this allows an AI to ask "is there an Enemy nearby?" and get a match for any
///     object whose Identity belongs to the "Enemy" category.
/// </summary>
/// <remarks>
///     Crucially, because it is a Resource, it can contain its own data, such as default relationships
///     to other categories, which a simple string tag (like a Godot Group) cannot do.
/// </remarks>
[GlobalClass, Tool]
public partial class Category : Resource
{
    /// <summary>
    ///     The user-friendly name of the category for debugging and editor purposes (e.g., "Enemy", "Item", "Consumable").
    /// </summary>
    [Export]
    public string CategoryName { get; private set; } = "Unnamed Category";

    /// <summary>
    ///     Optional decay strategy override for perception. When set, sensors will use this
    ///     strategy instead of their default for entities belonging to this category.
    /// </summary>
    [Export]
    public MemoryDecayStrategy? PerceptionDecay { get; private set; }

    #region Test Helpers

    /// <summary>Sets CategoryName for testing purposes.</summary>
    internal void SetCategoryName(string value) => CategoryName = value;

    /// <summary>Sets PerceptionDecay for testing purposes.</summary>
    internal void SetPerceptionDecay(MemoryDecayStrategy? value) => PerceptionDecay = value;

    #endregion
}
