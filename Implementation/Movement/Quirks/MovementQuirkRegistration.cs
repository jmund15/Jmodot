namespace Jmodot.Implementation.Movement.Quirks;

using System.Linq;
using AI.BB;
using Core.AI.BB;
using Core.Movement.Quirks;
using Shared.GodotExceptions;
using GColl = Godot.Collections;

/// <summary>
/// One owner's hold on a <see cref="MovementQuirkProcessor3D" />'s refcounted registry, tracked per
/// instance so a State and a BehaviorAction can share the discipline without sharing the state.
/// </summary>
/// <remarks>
/// <para>
/// The single invariant centralized here is that an owner only ever releases the registrations it
/// took itself. An owner's exit path runs on more paths than its entry path does — a BehaviorTask's
/// <c>Exit</c> fires <c>OnExit</c> even when <c>Enter</c> bailed on a failed condition, and
/// <see cref="Jmodot.Implementation.AI.HSM.CompoundState" />'s <c>OnExit</c> calls
/// <c>PrimarySubState?.Exit()</c> against a substate it deliberately never nulls, so history states
/// can resume. An unconditional unregister on either path decrements a refcount this owner never
/// incremented and drops a co-holder's live registration, silently.
/// </para>
/// <para>
/// <see cref="Release" /> walks a SNAPSHOT taken at <see cref="Register" />, never the owner's live
/// array. The authored array is mutable behind a private setter and reachable from test seams, so an
/// element added between entry and exit would otherwise be unregistered by an owner that never
/// registered it — reintroducing, through the back door, the exact defect this type closes.
/// </para>
/// <para>
/// A struct, and a plain field on the owning node (<c>private MovementQuirkRegistration _quirks;</c>)
/// — mutating through a property or a <c>readonly</c> field would mutate a copy.
/// </para>
/// </remarks>
public struct MovementQuirkRegistration
{
    private MovementQuirkProcessor3D? _processor;
    private GColl.Array<MovementQuirk3D>? _quirks;
    private bool _suspendDrive;

    /// <summary>What <see cref="Register" /> actually took, and all <see cref="Release" /> may drop.</summary>
    private MovementQuirk3D[]? _held;

    /// <summary>True when this owner took the processor's scoped drive suspension.</summary>
    private bool _heldSuspension;

    /// <summary>True between a <see cref="Register" /> and its matching <see cref="Release" />.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>
    /// Bind the owner's authored array and requested drive suspension, and resolve the processor
    /// from the blackboard. Safe to call again after either changes: a live hold is released first,
    /// so the rebind can never strand references or a suspension against the old configuration.
    /// A processor resolves when quirks are assigned OR suspension is requested, so an empty array
    /// does not make a step's gait opt-out silently inert.
    /// </summary>
    /// <exception cref="NodeConfigurationException">
    /// Quirks or a drive suspension are requested but the agent has no
    /// <see cref="MovementQuirkProcessor3D" />.
    /// </exception>
    public void Resolve(IBlackboard? bb, GColl.Array<MovementQuirk3D> quirks, Node owner,
        bool suspendDrive = false)
    {
        this.Release();
        this._processor = null;
        this._quirks = null;
        this._suspendDrive = suspendDrive;

        if (quirks.Count == 0 && !suspendDrive)
        {
            this._quirks = quirks;
            return;
        }

        if (bb == null
            || !bb.TryGet<MovementQuirkProcessor3D>(BBDataSig.MovementQuirkProcessor, out var processor)
            || processor == null)
        {
            // Bind nothing: a caller that swallows this must not be left holding an array or a
            // suspension request whose processor never resolved.
            var capability = quirks.Count > 0
                ? (suspendDrive ? "MovementQuirks and SuspendGait" : "MovementQuirks")
                : "SuspendGait";
            throw new NodeConfigurationException(
                $"'{owner.Name}' requests {capability} but the agent has no MovementQuirkProcessor3D.",
                owner);
        }

        this._quirks = quirks;
        this._processor = processor;
    }

    /// <summary>
    /// Take a reference on every authored quirk and, when requested, one drive suspension.
    /// Idempotent — a second call without an intervening <see cref="Release" /> is inert, so a
    /// re-entered owner cannot double-count itself.
    /// </summary>
    public void Register()
    {
        if (this.IsHeld) { return; }

        this._held = this._quirks is { Count: > 0 } ? this._quirks.ToArray() : System.Array.Empty<MovementQuirk3D>();
        this.IsHeld = true;

        foreach (var quirk in this._held)
        {
            this._processor?.RegisterQuirk(quirk);
        }

        if (this._suspendDrive)
        {
            this._processor?.SuspendDrive();
            this._heldSuspension = this._processor != null;
        }
    }

    /// <summary>
    /// Drop this owner's references and its drive suspension if it took one. Idempotent, and inert
    /// when this owner never registered — safe to call from an exit path that runs whether or not
    /// the matching entry path ever did.
    /// </summary>
    public void Release()
    {
        if (!this.IsHeld) { return; }

        var held = this._held;
        this._held = null;
        this.IsHeld = false;

        if (this._heldSuspension)
        {
            this._processor?.ResumeDrive();
            this._heldSuspension = false;
        }

        if (held == null) { return; }
        foreach (var quirk in held)
        {
            this._processor?.UnregisterQuirk(quirk);
        }
    }
}
