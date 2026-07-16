namespace Jmodot.Implementation.AI.HSM;

using Godot;
using Implementation.AI.BB;
using Implementation.Combat;
using Implementation.Combat.Status;
using Implementation.Visual.Effects;
using Jmodot.Core.Actors;
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

    /// <summary>
    /// Shared, project-authored tag→profile defaults. Consulted only when the active
    /// trigger tag has NO entry in this entity's own <see cref="ProfileMap"/>, so a new
    /// suppressing status can be added for every entity at once via a single data edit
    /// on the shared set rather than a per-entity ProfileMap entry.
    /// </summary>
    [Export] public SuppressionProfileSet? SharedDefaults { get; set; }

    private BehaviorAlterationProfile? _activeProfile;
    private bool _movementOverridePushed;
    private IMovementProcessor3D? _pushedProcessor;
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
                // Per-entity author intent wins first — including an explicit null value,
                // which deliberately resolves to DefaultProfile, NOT the shared set.
                return kvp.Value ?? DefaultProfile;
            }
        }

        // No per-entity ProfileMap entry for the active tag — consult the shared
        // tag→profile defaults so a new suppressing status is one edit on the shared
        // set instead of a per-entity ProfileMap addition on every entity.
        if (SharedDefaults != null)
        {
            foreach (var kvp in SharedDefaults.Defaults)
            {
                if (kvp.Key != null && statusComp.HasTag(kvp.Key))
                {
                    return kvp.Value ?? DefaultProfile;
                }
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
            if (BB.TryGet<IMovementProcessor3D>(BBDataSig.MovementProcessor, out var processor) && processor != null)
            {
                processor.SetStrategyOverride((IMovementStrategy3D)profile.MovementStrategyOverride);
                _pushedProcessor = processor;
                _movementOverridePushed = true;
            }
            else
            {
                JmoLogger.Error(this, "BehaviorSuppressedState has MovementStrategyOverride but BB.MovementProcessor is not registered.");
            }
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
            _pushedProcessor?.ClearStrategyOverride();
            _pushedProcessor = null;
            _movementOverridePushed = false;
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
