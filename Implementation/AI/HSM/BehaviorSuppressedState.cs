namespace Jmodot.Implementation.AI.HSM;

using Godot;
using Implementation.AI.BB;
using Implementation.Combat;
using Implementation.Combat.Status;
using Implementation.Visual.Effects;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Combat;
using Jmodot.Core.Movement.Strategies;
using Jmodot.Core.Visual.Animation.Sprite;
using Shared;
using GColl = Godot.Collections;

/// <summary>
/// Generic HSM state that an entity enters when any behavior-altering status
/// (freeze, stun, root, etc.) is active on its
/// <see cref="StatusEffectComponent"/>. Resolves which trigger tag is currently
/// active, looks up the corresponding <see cref="BehaviorAlterationProfile"/> in
/// <see cref="ProfileMap"/>, and applies its movement/visual/animation overrides
/// while the state is active.
/// </summary>
/// <remarks>
/// <para>
/// Entity wires ONCE: drop a BehaviorSuppressedState node into the HSM and
/// author the per-tag ProfileMap. New behavior-altering statuses are pure data
/// additions thereafter (new tag + new profile .tres + map entry).
/// </para>
/// <para>
/// For unique mechanics (button-mash escape, intensity scaling), subclass
/// <see cref="BehaviorAlterationProfile"/> instead of subclassing this state —
/// that keeps "what does the entity do?" in one generic dispatcher and "how
/// does this status uniquely behave?" in the polymorphic Profile family.
/// </para>
/// <para>
/// Transitions in/out of this state are wired via
/// <see cref="Examples.AI.HSM.TransitionConditions.StatusActiveAnyTagCondition"/>
/// (entry: any trigger tag active; exit: same condition with Inverted=true).
/// </para>
/// <para>
/// <b>MUTUAL EXCLUSIVITY with <c>MovementOverrideStatusRunner</c>:</b> both write
/// to <see cref="BBDataSig.ActiveMovementStrategy"/> and cache the prior on apply.
/// If a movement-feel-only status (slow/haste, runner-driven) overlaps with an
/// AI-suppressing status (freeze/stun, profile-driven via this state), the BB
/// ownership stack can break depending on exit order. Author tag categories so
/// the two cannot be simultaneously active on the same entity.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class BehaviorSuppressedState : State
{
    /// <summary>
    /// Per-trigger-tag profile. The state finds the first key whose tag is currently
    /// active on the StatusEffectComponent and uses its mapped profile. If no key
    /// matches but a tag IS active (legitimate gap), falls back to <see cref="DefaultProfile"/>.
    /// </summary>
    /// <remarks>
    /// Iteration order is dictionary-insertion order — for entities where multiple trigger
    /// tags can be simultaneously active (rare; e.g., freeze+stun stacked), first-inserted
    /// wins. Author profile maps with non-overlapping tag sets, or accept first-insertion-wins.
    /// Godot Inspector cannot edit Dictionary&lt;Resource, Resource&gt; exports — author entries
    /// directly in .tscn text (see Wizard/wizard.tscn for reference).
    /// </remarks>
    [Export] public GColl.Dictionary<CombatTag, BehaviorAlterationProfile> ProfileMap { get; set; } = new();

    /// <summary>
    /// Fallback profile used when ProfileMap doesn't contain an entry for the active
    /// trigger tag. Lets entities define a generic "any unmapped behavior-alter
    /// looks like X" without enumerating every tag.
    /// </summary>
    [Export] public BehaviorAlterationProfile? DefaultProfile { get; set; }

    private BehaviorAlterationProfile? _activeProfile;
    private IMovementStrategy3D? _priorMovementStrategy;
    private bool _movementOverridePushed;
    private VisualEffectController? _visualController;
    private bool _visualPlayed;
    private IAnimationOrchestrator? _animationOrchestrator;
    private bool _animationStarted;

    protected override void OnEnter()
    {
        base.OnEnter();
        _activeProfile = ResolveActiveProfile();
        if (_activeProfile == null) { return; }

        ApplyDefaults(_activeProfile);
        _activeProfile.OnSuppressionEnter(this);
    }

    protected override void OnProcessFrame(float delta)
    {
        base.OnProcessFrame(delta);
        _activeProfile?.OnSuppressionProcess(this, delta);
    }

    protected override void OnExit()
    {
        if (_activeProfile != null)
        {
            var profile = _activeProfile;
            _activeProfile = null;
            try
            {
                profile.OnSuppressionExit(this);
            }
            catch (System.Exception ex)
            {
                JmoLogger.Error(this, $"Profile OnSuppressionExit threw: {ex.Message}");
            }
            RestoreDefaults(profile);
        }
        base.OnExit();
    }

    private BehaviorAlterationProfile? ResolveActiveProfile()
    {
        if (!BB.TryGet<StatusEffectComponent>(BBDataSig.StatusEffects, out var statusComp) || statusComp == null)
        {
            return null;
        }

        foreach (var kvp in ProfileMap)
        {
            if (kvp.Key != null && statusComp.HasTag(kvp.Key))
            {
                return kvp.Value ?? DefaultProfile;
            }
        }

        // No mapped tag is active. Only fall back to default if ANY tag is
        // active (we wouldn't have entered this state otherwise) — but since
        // unit tests can call Enter directly, just return DefaultProfile when
        // no map entry matches. The transition framework guards entry timing.
        return DefaultProfile;
    }

    private void ApplyDefaults(BehaviorAlterationProfile profile)
    {
        if (profile.MovementStrategyOverride != null)
        {
            BB.TryGet<IMovementStrategy3D>(BBDataSig.ActiveMovementStrategy, out _priorMovementStrategy);
            BB.Set(BBDataSig.ActiveMovementStrategy, (IMovementStrategy3D)profile.MovementStrategyOverride);
            _movementOverridePushed = true;
        }

        if (profile.PersistentVisualEffect != null)
        {
            _visualController = ResolveVisualController();
            if (_visualController != null)
            {
                _visualController.PlayEffect(profile.PersistentVisualEffect);
                _visualPlayed = true;
            }
        }

        if (profile.AnimationOverride != null && !profile.AnimationOverride.IsEmpty)
        {
            if (BB.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out _animationOrchestrator)
                && _animationOrchestrator != null)
            {
                _animationOrchestrator.StartAnim(profile.AnimationOverride);
                _animationStarted = true;
            }
        }
    }

    private void RestoreDefaults(BehaviorAlterationProfile profile)
    {
        if (_movementOverridePushed)
        {
            BB.Set<IMovementStrategy3D?>(BBDataSig.ActiveMovementStrategy, _priorMovementStrategy);
            _movementOverridePushed = false;
            _priorMovementStrategy = null;
        }

        if (_visualPlayed && profile.PersistentVisualEffect != null && _visualController != null
            && GodotObject.IsInstanceValid(_visualController))
        {
            _visualController.StopEffect(profile.PersistentVisualEffect);
        }

        _visualPlayed = false;
        _visualController = null;

        if (_animationStarted && _animationOrchestrator != null)
        {
            _animationOrchestrator.StopAnim();
        }

        _animationStarted = false;
        _animationOrchestrator = null;
    }

    private VisualEffectController? ResolveVisualController()
    {
        if (BB.TryGet<VisualEffectController>(BBDataSig.VisualEffectController, out var vec))
        {
            return vec;
        }
        return null;
    }
}
