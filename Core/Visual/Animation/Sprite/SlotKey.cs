namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Strong identifier for a <c>VisualSlotNode</c>. Authored as <c>.tres</c> so designers
/// reference the same slot key across components instead of typing string names repeatedly.
/// Equality is on <see cref="Id"/>, so two <c>SlotKey</c> resources pointing at the same
/// <see cref="StringName"/> interchange in dictionary lookups.
/// </summary>
[GlobalClass]
public partial class SlotKey : Resource
{
    [Export, RequiredExport] public StringName Id { get; set; } = null!;
    [Export] public string DisplayName { get; set; } = "";

    public override bool Equals(object? obj)
        => obj is SlotKey other && other.Id == Id;

    public override int GetHashCode()
        => Id?.GetHashCode() ?? 0;

    public override string ToString()
        => string.IsNullOrEmpty(DisplayName) ? $"SlotKey({Id})" : $"SlotKey({Id}: {DisplayName})";
}
