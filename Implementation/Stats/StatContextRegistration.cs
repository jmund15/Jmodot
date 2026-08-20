namespace Jmodot.Implementation.Stats;

using Core.AI.BB;
using Core.Stats;
using AI.BB;

/// <summary>
/// One owner's hold on an <see cref="IStatProvider" />'s active-context set, tracked per instance so
/// an owner only ever removes the context it applied itself.
/// </summary>
/// <remarks>
/// <para>
/// Same invariant, same cause, as <see cref="Jmodot.Implementation.Movement.Quirks.MovementQuirkRegistration" />:
/// an owner's exit path runs on more paths than its entry path does, and
/// <see cref="Jmodot.Implementation.AI.HSM.CompoundState" />'s <c>OnExit</c> calls
/// <c>PrimarySubState?.Exit()</c> against a substate it deliberately never nulls so history states can
/// resume. The consequence is worse here than for quirks: the provider's context set is NOT
/// refcounted, and <c>RemoveActiveContext</c> drops every modifier keyed to that context instance
/// regardless of which owner contributed it. Two sibling states authoring one shared
/// <see cref="StatContext" /> — the live shape in <c>hoarder_npc.tscn</c> and <c>loot_box_npc.tscn</c>
/// — therefore let an unpaired exit on one strip the other's live modifiers.
/// </para>
/// <para>
/// A struct, and a plain field on the owning node — mutating through a property or a
/// <c>readonly</c> field would mutate a copy.
/// </para>
/// </remarks>
public struct StatContextRegistration
{
    private IStatProvider? _provider;
    private StatContext? _context;

    /// <summary>What <see cref="Apply" /> actually added, and all <see cref="Remove" /> may drop.</summary>
    private StatContext? _held;

    /// <summary>True between an <see cref="Apply" /> and its matching <see cref="Remove" />.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>
    /// Bind the owner's authored context and resolve its provider from the blackboard. Safe to call
    /// again after the context changes: a live hold is removed first, so the rebind can never strand
    /// the previous context on the provider.
    /// </summary>
    /// <remarks>
    /// An absent provider is not a configuration error the way an absent quirk processor is — a
    /// context authored on an entity with no stats is inert, not broken, and the base class already
    /// fails loud on genuinely required dependencies.
    /// </remarks>
    public void Resolve(IBlackboard? bb, StatContext? context)
    {
        this.Remove();

        this._context = context;
        this._provider = null;
        if (context == null || bb == null) { return; }

        if (bb.TryGet<IStatProvider>(BBDataSig.Stats, out var provider))
        {
            this._provider = provider;
        }
    }

    /// <summary>
    /// Apply the authored context. Idempotent — a second call without an intervening
    /// <see cref="Remove" /> is inert, so a re-entered owner cannot re-apply itself.
    /// </summary>
    public void Apply()
    {
        if (this.IsHeld) { return; }

        this._held = this._context;
        this.IsHeld = true;
        if (this._held == null) { return; }

        this._provider?.AddActiveContext(this._held);
    }

    /// <summary>
    /// Remove this owner's context. Idempotent, and inert when this owner never applied one — safe
    /// to call from an exit path that runs whether or not the matching entry path ever did.
    /// </summary>
    public void Remove()
    {
        if (!this.IsHeld) { return; }

        var held = this._held;
        this._held = null;
        this.IsHeld = false;

        if (held == null) { return; }
        this._provider?.RemoveActiveContext(held);
    }
}
