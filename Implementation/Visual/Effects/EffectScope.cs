namespace Jmodot.Implementation.Visual.Effects;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Visual.Effects;
using Godot;
using Jmodot.Implementation.Visual.Animation.Sprite;

/// <summary>
/// A lazy predicate over a <see cref="VisualComposer"/>'s nodes. Resolves to the
/// subset of visual nodes an effect should apply to — evaluated at query time, so
/// a long-running effect "follows" slot swaps automatically (e.g. a glow scoped to
/// <c>Slot("Hat")</c> covers whichever hat is currently equipped).
/// </summary>
/// <remarks>
/// Use the static factories (<see cref="All"/>, <see cref="Slot"/>,
/// <see cref="AllExceptSlot"/>, <see cref="Nodes"/>, <see cref="Provider"/>) rather
/// than constructing directly. Equality is value-based for simple kinds (All,
/// Slot name, AllExceptSlot name) so scope-keyed effect dictionaries behave.
/// <c>Nodes</c> and <c>Provider</c> use reference equality on the captured list /
/// provider — re-creating the scope with the same nodes produces a distinct key.
/// <para>
/// Lives in Implementation (not Core) because it knows about
/// <see cref="VisualComposer.TryGetSlot"/>. If a Core-layer scope abstraction is
/// ever needed, it can accept any <see cref="IVisualSpriteProvider"/>.
/// </para>
/// </remarks>
public sealed class EffectScope : IEquatable<EffectScope>
{
    private enum ScopeKind { All, Slot, AllExceptSlot, Nodes, Provider }

    private readonly ScopeKind _kind;
    private readonly string? _slotName;
    private readonly IReadOnlyList<Node>? _explicitNodes;
    private readonly IVisualSpriteProvider? _provider;

    private EffectScope(
        ScopeKind kind,
        string? slotName = null,
        IReadOnlyList<Node>? nodes = null,
        IVisualSpriteProvider? provider = null)
    {
        this._kind = kind;
        this._slotName = slotName;
        this._explicitNodes = nodes;
        this._provider = provider;
    }

    /// <summary>Every node the composer knows about.</summary>
    public static EffectScope All => new(ScopeKind.All);

    /// <summary>Just the named slot's nodes. Resolves empty if the slot doesn't exist.</summary>
    public static EffectScope Slot(string slotName) => new(ScopeKind.Slot, slotName: slotName);

    /// <summary>Every node EXCEPT those owned by the named slot.</summary>
    public static EffectScope AllExceptSlot(string slotName) => new(ScopeKind.AllExceptSlot, slotName: slotName);

    /// <summary>
    /// An explicit, context-independent node list. Snapshotted at construction —
    /// the composer is ignored during resolution. Useful for nodes that don't live
    /// inside any slot (e.g. a wizard's free-hand sprites).
    /// </summary>
    public static EffectScope Nodes(IEnumerable<Node> nodes) =>
        new(ScopeKind.Nodes, nodes: nodes.ToList());

    /// <summary>An arbitrary sub-provider — often an in-prefab <see cref="IVisualSpriteProvider"/>.</summary>
    public static EffectScope Provider(IVisualSpriteProvider provider) =>
        new(ScopeKind.Provider, provider: provider);

    /// <summary>
    /// Resolves to the current node set for this scope. Called each time the effect
    /// controller iterates — this is what makes scopes "follow" slot changes.
    /// </summary>
    public IEnumerable<Node> Resolve(VisualComposer composer)
    {
        switch (this._kind)
        {
            case ScopeKind.All:
                return composer.GetAllVisualNodes();

            case ScopeKind.Slot:
                return composer.TryGetSlot(this._slotName!, out var slot) && slot != null
                    ? slot.GetAllVisualNodes()
                    : Array.Empty<Node>();

            case ScopeKind.AllExceptSlot:
                if (!composer.TryGetSlot(this._slotName!, out var excludedSlot) || excludedSlot == null)
                {
                    return composer.GetAllVisualNodes();
                }
                var excluded = new HashSet<Node>(excludedSlot.GetAllVisualNodes());
                return composer.GetAllVisualNodes().Where(n => !excluded.Contains(n));

            case ScopeKind.Nodes:
                return this._explicitNodes!;

            case ScopeKind.Provider:
                return this._provider!.GetAllVisualNodes();

            default:
                throw new InvalidOperationException($"Unknown EffectScope kind: {this._kind}");
        }
    }

    public bool Equals(EffectScope? other)
    {
        if (other is null) { return false; }
        if (this._kind != other._kind) { return false; }
        return this._kind switch
        {
            ScopeKind.All => true,
            ScopeKind.Slot or ScopeKind.AllExceptSlot => this._slotName == other._slotName,
            ScopeKind.Nodes => ReferenceEquals(this._explicitNodes, other._explicitNodes),
            ScopeKind.Provider => ReferenceEquals(this._provider, other._provider),
            _ => false,
        };
    }

    public override bool Equals(object? obj) => obj is EffectScope s && this.Equals(s);

    public override int GetHashCode() => this._kind switch
    {
        ScopeKind.All => 1,
        ScopeKind.Slot => HashCode.Combine(2, this._slotName),
        ScopeKind.AllExceptSlot => HashCode.Combine(3, this._slotName),
        ScopeKind.Nodes => HashCode.Combine(4, this._explicitNodes),
        ScopeKind.Provider => HashCode.Combine(5, this._provider),
        _ => 0,
    };

    public override string ToString() => this._kind switch
    {
        ScopeKind.All => "All",
        ScopeKind.Slot => $"Slot({this._slotName})",
        ScopeKind.AllExceptSlot => $"AllExceptSlot({this._slotName})",
        ScopeKind.Nodes => $"Nodes[{this._explicitNodes?.Count ?? 0}]",
        ScopeKind.Provider => "Provider",
        _ => "?",
    };
}
