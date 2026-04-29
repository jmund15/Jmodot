namespace Jmodot.Core.Visual.Effects;

using System;
using Godot;
using Jmodot.Core.Visual;

/// <summary>
/// Owns per-node base-color state and persistent tints scoped by <see cref="VisualQuery"/>.
/// Replaces the legacy <c>BaseModulationTracker</c> with a query-driven, hot-swap-aware
/// surface: a tint registered via <see cref="TintByQuery"/> automatically reapplies to
/// any node added later that matches the query (e.g. swap weapon → new weapon inherits
/// the persistent right-hand tint).
/// </summary>
public interface IVisualEffectService
{
    /// <summary>
    /// Low-level: register a node's intended base color. Called by <c>VisualSlotNode</c>
    /// during <c>Equip</c> for every spawned sprite. Idempotent.
    /// </summary>
    void RegisterBaseColor(Node node, Color color);

    /// <summary>
    /// Low-level: remove a node's base-color registration. Called by
    /// <c>VisualSlotNode</c> during <c>Unequip</c>. Idempotent.
    /// </summary>
    void UnregisterSprite(Node node);

    /// <summary>
    /// Returns the registered base color for a node, or <see cref="Colors.White"/> if
    /// no registration exists.
    /// </summary>
    Color GetBaseColor(Node node);

    /// <summary>Try-get variant — distinguishes "registered as white" from "not registered."</summary>
    bool TryGetBaseColor(Node node, out Color baseColor);

    /// <summary>
    /// Register a persistent tint applied to every node matching <paramref name="query"/>
    /// — both currently-present nodes AND nodes added in the future. Returns an
    /// <see cref="EffectId"/> usable with <see cref="RemoveTint"/>.
    /// </summary>
    EffectId TintByQuery(VisualQuery query, Color color);

    /// <summary>Remove a previously-registered persistent tint. Idempotent.</summary>
    void RemoveTint(EffectId id);

    /// <summary>
    /// Fired whenever a node's effective base color changes (registration, tint apply,
    /// tint clear). <c>VisualEffectController</c> subscribes to rebuild its blend cache.
    /// </summary>
    event Action<Node, Color> TintChanged;
}

/// <summary>Opaque token returned from <see cref="IVisualEffectService.TintByQuery"/>.</summary>
public readonly record struct EffectId(long Value)
{
    public static EffectId None => new(0);
}
