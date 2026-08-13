namespace Jmodot.Implementation.AI.Animation;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.AI;
using Core.Components;
using Core.Movement;
using Core.Visual.Animation.Sprite;
using Godot;
using HSM;
using BB;
using BehaviorTree;
using BehaviorTree.Composites;
using BehaviorTree.Tasks;
using Shared;

/// <summary>
///     The one animation authority an animated entity mounts: it answers who owns the animator right
///     now, innermost-first over nested scopes — BT task ⊂ HSM state ⊂ entity fallback — and pushes
///     the winner onto the base layer. Any active <see cref="IAnimatedState" /> claimant owns the body
///     and starts its own clip; the resolver re-pushes the winner once whenever the RESOLUTION
///     changes, so a claim handoff (a leaf exiting under a still-claiming state, or a mis-ordered
///     enter) self-heals within one physics frame instead of stranding the previous clip.
///     <para>
///     Drop under an entity's Visuals node beside its AnimationOrchestrator. Two modes: a null
///     <see cref="FallbackProfile" /> makes it arbitration-only — every frame no scope claims resolves
///     to nothing and the last clip simply persists; a profile assigned means a character controller
///     is required and the profile's clips are validated against the entity's animator. Without a
///     resolver, an entity whose states never claim renders nothing at all, because
///     AnimationVisibilityCoordinator hides every Vis_ node until the first AnimStarted.
///     </para>
///     <para>
///     The runtime binds the blackboard-provisioned orchestrator; the editor-time warning walk is
///     advisory and first-match. The two can only diverge on an entity carrying two
///     Phase-0-provisioning orchestrators, which is already a scene-authoring defect the entity
///     initializer warns about.
///     </para>
/// </summary>
[GlobalClass, Tool]
public partial class AnimationClaimResolver : Node, IComponent
{
    /// <summary>
    ///     The entity's HSM root. Leave empty on an HSM-less animated prop: both claim tiers go inert
    ///     and only the fallback profile drives the body.
    /// </summary>
    [Export] public CompoundState? HsmRoot { get; private set; }

    /// <summary>
    ///     What the body looks like on frames nobody claims. Leave empty for arbitration-only, the
    ///     shape for an entity whose leaf states claim 1:1.
    /// </summary>
    [Export] public AnimationFallbackProfile3D? FallbackProfile { get; private set; }

    // A structural property of the BT tick boundary, not an entity tunable: a composite legitimately
    // reports no running child for one tick, and the A->null->A flap would restart the claimant's clip.
    private const int TaskClaimGraceFrames = 1;

    private IAnimationOrchestrator? _anim;
    private ICharacterController3D? _controller;

    private ClaimHold _taskHold;
    private StringName? _fallbackClip;
    private StringName? _lastResolved;
    private State? _cachedTreeState;
    private BehaviorTree? _cachedTree;

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public bool Initialize(IBlackboard bb)
    {
        if (this.IsInitialized)
        {
            return true;
        }

        if (this.HsmRoot == null && this.FallbackProfile == null)
        {
            JmoLogger.Error(this,
                "[Visual] AnimationClaimResolver has neither an HsmRoot nor a FallbackProfile, so it can "
                + "resolve nothing at all. Assign one, or remove the node.");
            return false;
        }

        // Every IBlackboardProvider publishes before any component's Initialize runs, so a miss here
        // is permanent — a lazy retry would only delay the same failure past the frames that matter.
        if (!bb.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out var anim) || anim == null)
        {
            JmoLogger.Error(this,
                "[Visual] AnimationClaimResolver found no AnimationOrchestrator on the entity blackboard; "
                + "animation arbitration is disabled for this entity.");
            return false;
        }
        this._anim = anim;

        if (this.FallbackProfile != null)
        {
            if (!bb.TryGet<ICharacterController3D>(BBDataSig.CharacterController, out var controller) || controller == null)
            {
                JmoLogger.Error(this,
                    "[Visual] AnimationClaimResolver carries a FallbackProfile but found no CharacterController "
                    + "on the entity blackboard; the fallback tier cannot read the body's speed.");
                return false;
            }
            this._controller = controller;

            var invalid = this.FallbackProfile.ValidateConfiguration();
            if (invalid != null)
            {
                // Both channels deliberately: a .tres-only edit never opens the scene that would show
                // the dock warning, and a headless run never opens a scene at all.
                JmoLogger.Error(this, $"[Visual] AnimationClaimResolver FallbackProfile is misconfigured: {invalid}");
            }

            foreach (var clip in this.FallbackProfile.AuthoredClips)
            {
                if (this._anim.HasAnimationBase(clip))
                {
                    continue;
                }
                JmoLogger.Error(this,
                    $"[Visual] AnimationClaimResolver FallbackProfile authors clip '{clip}', which this entity's "
                    + "animator does not provide.");
            }
        }

        this.IsInitialized = true;
        this.Initialized.Invoke();
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !this.IsInitialized || this._anim == null)
        {
            return;
        }

        var stateLeaf = this.ResolveActiveStateLeaf();

        // The task tier is sampled, not committed: debounce it. The state tier is not — HSM
        // transitions are committed, so a grace there would delay every real handoff by a frame.
        this._taskHold = LocomotionAnimResolver.AdvanceClaim(
            this._taskHold, ClaimOf(this.ResolveActiveTaskLeaf(stateLeaf)), TaskClaimGraceFrames);
        var taskClaim = this._taskHold.Held;
        var stateClaim = ClaimOf(stateLeaf);
        var claimantWon = taskClaim is { IsEmpty: false } || stateClaim is { IsEmpty: false };

        // The fallback tier advances even while a claimant owns the body, so the hand-back reads the
        // entity's real motion instead of whatever it was doing when the claim started.
        this._fallbackClip = this.FallbackProfile != null && this._controller != null
            ? this.FallbackProfile.ResolveClip(this._controller, (float)delta, this._fallbackClip)
            : null;

        var resolved = LocomotionAnimResolver.ResolveBaseAnim(taskClaim, stateClaim, this._fallbackClip);
        var resolutionChanged = resolved != this._lastResolved;
        this._lastResolved = resolved;

        if (resolved == null)
        {
            // Zero push, not an empty base: an empty push fires the orchestrator's deferred
            // missing-clip AnimFinished failsafe and falsely completes every AnimFinished-gated state.
            return;
        }

        if (claimantWon)
        {
            // A claimant won. It started its own clip in OnEnter, so the push fires only on the frame
            // the RESOLUTION changes — that heals the two claim-handoff gaps (an animated leaf exiting
            // under a still-claiming state; a wrong outer-before-inner enter order) within one frame.
            // Never per-frame: StartAnimIfChanged deliberately restarts FINISHED clips (load-bearing
            // for locomotion stale-mirror recovery), which would re-trigger an AnimFinished-gated
            // claimant's one-shot every frame after it ends.
            if (resolutionChanged)
            {
                this._anim.StartAnimIfChanged(resolved);
            }
            return;
        }

        // StartAnimIfChanged owns the start-on-change decision for every base-layer caller; see its
        // remarks for why both halves of that guard are load-bearing.
        this._anim.StartAnimIfChanged(resolved);
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.HsmRoot == null && this.FallbackProfile == null)
        {
            warnings.Add(
                "Neither HsmRoot nor FallbackProfile is assigned, so this resolver can never resolve a "
                + "clip. Assign an HSM root for claim arbitration, a fallback profile for locomotion, or both.");
        }

        if (!this.TryFindOrchestrator(out var orchestrator) || orchestrator == null)
        {
            warnings.Add("No AnimationOrchestrator found on this entity; the resolver has nothing to push to.");
        }

        if (this.HsmRoot == null && ConfigWarnings.TryFindEntitySibling<CompoundState>(this, out _))
        {
            warnings.Add(
                "HsmRoot is empty but this entity carries a state machine, so every state claim is "
                + "invisible to the resolver. Assign the HSM root, or delete the state machine.");
        }

        if (this.FallbackProfile == null)
        {
            return warnings.ToArray();
        }

        if (!ConfigWarnings.TryFindEntitySibling<CharacterBody3D>(this, out _) && this.Owner is not CharacterBody3D)
        {
            warnings.Add(
                "A FallbackProfile is assigned but this entity has no character body, so the fallback "
                + "tier cannot read the body's speed.");
        }

        var invalid = this.FallbackProfile.ValidateConfiguration();
        if (invalid != null)
        {
            warnings.Add(invalid);
        }

        if (orchestrator == null)
        {
            return warnings.ToArray();
        }

        foreach (var clip in this.FallbackProfile.AuthoredClips)
        {
            if (orchestrator.HasAnimationBase(clip))
            {
                continue;
            }
            warnings.Add($"FallbackProfile authors clip '{clip}', which this entity's animator does not provide.");
        }

        return warnings.ToArray();
    }

    private bool TryFindOrchestrator(out IAnimationOrchestrator? orchestrator)
    {
        for (var ancestor = this.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
        {
            if (ancestor.TryGetFirstChildOfInterface<IAnimationOrchestrator>(out orchestrator) && orchestrator != null)
            {
                return true;
            }

            if (ancestor.TryGetFirstChildOfInterface<IBlackboard>(out _, includeSubChildren: false))
            {
                break;
            }
        }

        orchestrator = null;
        return false;
    }

    /// <summary>
    ///     Descends PrimarySubState to the active leaf. Stops on a null link (the root has not
    ///     entered yet — this resolver's physics tick can precede it) and rejects an inactive leaf:
    ///     CompoundState.OnExit deliberately leaves PrimarySubState pointing at the last substate to
    ///     support history states, so after the HSM exits, a stale leaf would read as a live
    ///     claimant and silence this resolver permanently.
    /// </summary>
    private State? ResolveActiveStateLeaf()
    {
        State? current = this.HsmRoot;
        while (current is CompoundState compound)
        {
            var next = compound.PrimarySubState;
            if (next == null || !next.IsActive)
            {
                break;
            }
            current = next;
        }
        return current != null && current.IsActive ? current : null;
    }

    /// <summary>
    ///     The active BT leaf is only reachable through the active state — an entity carries one
    ///     BehaviorTree per BTState, so "the active tree" is undefined without it. The tree lookup is a
    ///     scene-tree query that marshals a fresh child array out of native, so it is cached and redone
    ///     only when the HSM hands the body to a different state; the descent below reads the cached
    ///     ChildTasks lists and allocates nothing.
    /// </summary>
    private BehaviorTask? ResolveActiveTaskLeaf(State? stateLeaf)
    {
        if (stateLeaf is not BTState)
        {
            return null;
        }
        if (!ReferenceEquals(stateLeaf, this._cachedTreeState))
        {
            this._cachedTreeState = stateLeaf;
            this._cachedTree = stateLeaf.TryGetFirstChildOfType(out BehaviorTree? found) ? found : null;
        }
        if (this._cachedTree == null)
        {
            return null;
        }

        BehaviorTask? current = this._cachedTree.RootTask;
        while (current is CompositeTask composite)
        {
            BehaviorTask? running = null;
            var children = composite.ChildTasks;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Status != TaskStatus.Running)
                {
                    continue;
                }
                running = children[i];
                break;
            }
            if (running == null)
            {
                break;
            }
            current = running;
        }
        return current;
    }

    private static StringName? ClaimOf(Node? node)
    {
        return node is IAnimatedState { IsAnimated: true } animated ? animated.AnimationName : null;
    }

    #region Test Helpers
#if TOOLS
    internal void SetForTest(CompoundState? hsmRoot, AnimationFallbackProfile3D? fallbackProfile)
    {
        this.HsmRoot = hsmRoot;
        this.FallbackProfile = fallbackProfile;
    }
#endif
    #endregion
}
