namespace Jmodot.Core.Visual.Animation.Sprite;

using Godot;
using Jmodot.Core.Shared.Attributes;

/// <summary>
/// Strong identifier for a <c>VisualSlotNode</c>. Authored as <c>.tres</c> so designers
/// reference the same slot key across components instead of typing string names repeatedly.
/// Equality is on <see cref="Id"/>, so two <c>SlotKey</c> resources pointing at the same
/// <see cref="StringName"/> interchange in dictionary lookups.
/// </summary>
/// <remarks>
/// <b>Naming convention:</b> SlotKey instances are referenced exclusively via
/// <c>[Export]</c> fields wired to <c>.tres</c> assets — never constructed inline with
/// a string-literal <see cref="Id"/>. This means the <see cref="Id"/> can be human-readable
/// with spaces (e.g. <c>"Right Hand"</c>, <c>"Left Hand"</c>) without risking
/// inline-string-mismatch bugs. If a future consumer needs to construct a SlotKey from
/// a code path that doesn't have access to the asset, audit it carefully — the typed
/// asset reference is the supported pattern.
/// </remarks>
[GlobalClass, Tool]
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
