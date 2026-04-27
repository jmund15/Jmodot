namespace Jmodot.Implementation.Combat.Status;

using Godot;
using Implementation.AI.HSM;
using Implementation.Movement.Strategies;
using Jmodot.Core.Visual.Effects;

/// <summary>
/// Data bundle + virtual lifecycle hooks describing how an entity reacts when a
/// behavior-altering status (freeze/stun/root/etc.) is applied. Authored as a
/// .tres per (entity × tag) and slotted into <see cref="BehaviorSuppressedState"/>'s
/// ProfileMap by the entity's designer.
/// </summary>
/// <remarks>
/// <para>
/// The base class is pure data — the only behavior is "push my movement override
/// to BB" (handled by <see cref="BehaviorSuppressedState"/> using these exports).
/// For unique mechanics (button-mash escape, intensity scaling, transformation),
/// subclass and override the lifecycle hooks. Existing entities pick up the
/// custom mechanic the moment they swap the .tres in their ProfileMap; no entity
/// code changes required.
/// </para>
/// <para>
/// <b>Resources are shared.</b> Do NOT cache per-instance state on the Profile —
/// multiple entities using the same .tres will overwrite each other. Per-instance
/// runtime state (e.g., prior movement strategy to restore) lives on the consuming
/// <see cref="BehaviorSuppressedState"/>; the Profile receives the state instance
/// in its hooks and may read/write its fields if needed.
/// </para>
/// </remarks>
[GlobalClass]
public partial class BehaviorAlterationProfile : Resource
{
    /// <summary>
    /// Optional movement strategy pushed to BB[ActiveMovementStrategy] while this
    /// profile is active. Null = no movement override.
    /// </summary>
    [Export] public BaseMovementStrategy3D? MovementStrategyOverride { get; set; }

    /// <summary>
    /// Optional visual effect applied via the entity's VisualEffectController on
    /// suppression enter (typically a tint — blue for freeze, yellow for stun).
    /// </summary>
    [Export] public VisualEffect? PersistentVisualEffect { get; set; }

    /// <summary>
    /// Optional animation name played via the entity's AnimationOrchestrator
    /// on suppression enter. Null = no animation override.
    /// </summary>
    [Export] public StringName? AnimationOverride { get; set; }

    /// <summary>
    /// Hook called when the entity enters its <see cref="BehaviorSuppressedState"/>
    /// and this profile is the active one. Default implementation: no-op (the
    /// state handles MovementStrategyOverride/PersistentVisualEffect/AnimationOverride
    /// based on the export values). Override for custom behavior.
    /// </summary>
    public virtual void OnSuppressionEnter(BehaviorSuppressedState state) { }

    /// <summary>
    /// Per-frame hook called while the entity is in <see cref="BehaviorSuppressedState"/>
    /// and this profile is active. Default: no-op. Override for ticking behavior
    /// (e.g., mash-escape decay, intensity scaling).
    /// </summary>
    public virtual void OnSuppressionProcess(BehaviorSuppressedState state, float delta) { }

    /// <summary>
    /// Hook called when the entity exits its <see cref="BehaviorSuppressedState"/>.
    /// Default: no-op. Override to clean up subclass-specific runtime state.
    /// </summary>
    public virtual void OnSuppressionExit(BehaviorSuppressedState state) { }
}
