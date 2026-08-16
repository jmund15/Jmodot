namespace Jmodot.Implementation.AI.BehaviorTree.Tasks;

using System;
using System.Collections.Generic;
using Animation;
using BB;
using Core.AI;
using Core.AI.BB;
using Core.AI.BehaviorTree;
using Core.Visual.Animation.Sprite;
using Godot;
using Shared;

/// <summary>
/// BT leaf that CLAIMS a clip for as long as it runs, and reports Success when that clip finishes.
/// </summary>
/// <remarks>
/// <para>
/// It claims, it never pushes: the entity's <see cref="AnimationClaimResolver" /> samples the claim
/// every physics frame and owns the base-layer write, so an orchestrator held here would be a second
/// writer racing arbitration. That makes the resolver a hard dependency — without one mounted, the
/// clip is never started and nothing will ever raise the AnimFinished this action waits on, so the
/// action fails loudly on enter rather than hanging Running forever.
/// </para>
/// <para>
/// An empty <see cref="Clip" /> claims nothing, so the enclosing state (or the entity's fallback
/// tier) keeps the body — but nothing then raises the AnimFinished this action waits on either, so
/// the action holds Running for as long as its parent lets it. It is a silent leaf, never a wait
/// shape; the configuration warning says so.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class PlayAnimAction : BehaviorAction, IAnimatedState
{
    /// <summary>
    /// Base clip claimed while this action runs. Leave empty to claim nothing and let the enclosing
    /// scope keep the body.
    /// </summary>
    [Export] public StringName Clip { get; private set; } = new();

    /// <inheritdoc />
    public bool IsAnimated => !this.Clip.IsEmpty;

    /// <inheritdoc />
    public StringName? AnimationName => this.Clip;

    /// <summary>
    /// Always null — see the class remarks: this action claims without performing, so it holds no
    /// performance handle for anything to write through.
    /// </summary>
    public IAnimationOrchestrator? AnimationOrchestrator => null;

    private IAnimationOrchestrator? _animator;
    private bool _subscribed;

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        bb.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out this._animator);
    }

    protected override void OnEnter()
    {
        base.OnEnter();

        if (!ConfigWarnings.TryFindEntitySibling<AnimationClaimResolver>(this, out _))
        {
            JmoLogger.Error(this,
                $"[AI] PlayAnimAction '{this.Name}' has no AnimationClaimResolver on its entity, so its "
                + "claim is never pushed and the AnimFinished it waits on would never arrive.");
            this.Status = TaskStatus.Failure;
            return;
        }

        if (this._animator == null)
        {
            JmoLogger.Error(this,
                $"[AI] PlayAnimAction '{this.Name}' found no AnimationOrchestrator on the entity "
                + "blackboard, so it cannot observe its clip completing.");
            this.Status = TaskStatus.Failure;
            return;
        }

        if (this.Clip.IsEmpty)
        {
            // Held Running, not completed: BehaviorTree's status handler re-enters a root task
            // synchronously on ANY terminal status set inside Enter, so completing here recurses
            // without bound. Until that restart defers a tick, the stall is warned, never fixed.
            JmoLogger.Warning(this,
                $"[AI] PlayAnimAction '{this.Name}' has an empty Clip: it claims nothing and will hold "
                + "Running until its parent aborts it.");
        }

        this._animator.AnimFinished += this.OnAnimFinished;
        this._subscribed = true;
        // The claim never re-resolves for a re-entered action (its Clip is unchanged), so the
        // resolver-side gate can't fire to re-drive a finished clip — drive it here before Running.
        if (!this.Clip.IsEmpty) { this._animator.StartAnimIfChanged(this.Clip); }
        this.Status = TaskStatus.Running;
    }

    protected override void OnExit()
    {
        if (this._subscribed && this._animator != null)
        {
            this._animator.AnimFinished -= this.OnAnimFinished;
        }
        this._subscribed = false;
        base.OnExit();
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>(base._GetConfigurationWarnings() ?? Array.Empty<string>());

        if (this.Clip.IsEmpty)
        {
            warnings.Add(
                "Clip is empty, so this action claims nothing and waits on an AnimFinished that never "
                + "arrives — it holds Running until its parent aborts it. Author a clip, or delete the action.");
        }

        if (!ConfigWarnings.TryFindEntitySibling<AnimationClaimResolver>(this, out _))
        {
            warnings.Add(
                "This entity mounts no AnimationClaimResolver, so this action's claim is never pushed and "
                + "the clip it waits on never plays. Add a resolver under the entity's Visuals node.");
        }

        if (!this.Clip.IsEmpty && this.TryFindOrchestrator(out var orchestrator) && orchestrator != null
            && !orchestrator.HasAnimationBase(this.Clip))
        {
            warnings.Add(
                $"This action claims clip '{this.Clip}', which this entity's animator does not provide. "
                + "The missing-clip failsafe then synthesizes Success for a clip that never played — "
                + "author a clip the animator owns, or correct the claim.");
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

    private void OnAnimFinished(StringName finished)
    {
        // The orchestrator reports the RESOLVED clip, which carries a direction suffix when the
        // library authors one, so a claim on the base name matches by prefix.
        if (this.Clip.IsEmpty || !finished.ToString().StartsWith(this.Clip.ToString(), StringComparison.Ordinal))
        {
            return;
        }
        this.Status = TaskStatus.Success;
    }

    #region Test Helpers
#if TOOLS
    internal void SetForTest(StringName clip) => this.Clip = clip;
#endif
    #endregion
}
