using Godot;
using Jmodot.Core.Combat;
using Jmodot.Core.Combat.Status;

namespace Jmodot.Implementation.Combat.Status;

/// <summary>
/// Status runner whose life clock is an integrity pool rather than a timer. The pool is authored
/// directly, drains at <see cref="DecayDps"/> every physics frame, and ends the status when it hits
/// zero. Incoming damage chips that same pool through <see cref="ApplyIntegrityDamage"/>, so "how
/// long does this last" and "how much punishment does it absorb" are one authored quantity instead
/// of two that drift apart.
///
/// Duration is derived and stored nowhere: <c>integrity / DecayDps</c>. Authoring the pool rather
/// than the duration is what keeps the two independent — with duration authored, any factor that
/// scales the decay rate also scales the pool, so making something decay faster would also make it
/// tougher to break.
/// </summary>
public abstract partial class IntegrityStatusRunner : StatusRunner
{
    /// <summary>
    /// Integrity lost per second under nominal conditions, before any multiplier the subclass
    /// resolves. The only rate on this type — everything else is a pool size.
    /// </summary>
    [Export] private float _baseDecayDps = 20f;

    public float MaxIntegrity { get; private set; }
    public float CurrentIntegrity { get; private set; }
    public float DecayDps { get; private set; }

    protected bool IsEnded { get; private set; }

    /// <summary>Sets the pool. The authored value IS the integrity — nothing derives it.</summary>
    protected void SetupIntegrity(float integrity)
    {
        MaxIntegrity = integrity;
        CurrentIntegrity = integrity;
    }

    public override void Start(ICombatant target, HitContext context)
    {
        base.Start(target, context);
        RecomputeDecayDps();
    }

    /// <summary>
    /// Re-resolves <see cref="DecayDps"/> from the base rate and the current multiplier.
    /// Deliberately not public: a public recompute is precisely the affordance that would let an
    /// ambient system reach into the runner and invert the dependency. A subclass that reacts to an
    /// ambient change calls this from its own handler, so the runner still pulls.
    /// </summary>
    protected void RecomputeDecayDps() => DecayDps = _baseDecayDps * ResolveDecayMultiplier(Target);

    /// <summary>
    /// Multiplier applied to the base decay rate; the base contributes nothing (1). This is the
    /// single composition point for everything that modulates decay — target resistance, ambient
    /// conditions — so none of those needs to add public surface to this type.
    /// </summary>
    protected virtual float ResolveDecayMultiplier(ICombatant target) => 1f;

    /// <summary>
    /// Chips integrity and reports whether the pool depleted. Does NOT end the status: what a
    /// depletion costs the target is the caller's to decide, so the decision is handed back rather
    /// than ending here and leaving the caller's consequences unapplied.
    /// </summary>
    public IntegrityDamageResult ApplyIntegrityDamage(float amount, IntegrityDamageSource source)
    {
        CurrentIntegrity -= amount;
        return new IntegrityDamageResult(CurrentIntegrity <= 0f, CurrentIntegrity);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsEnded) { return; }

        CurrentIntegrity -= DecayDps * (float)delta;
        if (CurrentIntegrity <= 0f)
        {
            OnIntegrityDepleted();
        }
    }

    /// <summary>
    /// Decay drained the pool. Implementors end the status with whatever reason fits their domain.
    /// </summary>
    protected abstract void OnIntegrityDepleted();

    /// <summary>
    /// Latches termination, returning true exactly once. Routing every termination path through this
    /// is what lets a subclass fire its end event once regardless of which path arrived first.
    /// </summary>
    protected bool TryLatchEnd()
    {
        if (IsEnded) { return false; }
        IsEnded = true;
        return true;
    }

    #region Test Helpers
#if TOOLS
    internal void _TestSetDecayDps(float v) => DecayDps = v;

    internal float _TestResolveDecayMultiplier(ICombatant target) => ResolveDecayMultiplier(target);
#endif
    #endregion
}
