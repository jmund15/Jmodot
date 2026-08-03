namespace Jmodot.Implementation.Visual.Sprite;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Core.Visual.Sprite;
using Jmodot.Implementation.Visual.Animation.Sprite;
using Shared;

/// <summary>
/// Facing-based horizontal mirroring for single-direction sprite art, driven by
/// clip RESOLUTION rather than per-sprite configuration: when an animator resolves
/// the undirected base clip (e.g. "run") for a directional request, its art is
/// single-direction and gets mirrored to match the facing; when it resolves a
/// directional variant (e.g. "hit_left"), the art is already facing-correct and
/// any mirror is cleared. No opt-in flags — the clip name IS the signal, so
/// flipping stops automatically per-animation as directional art is authored.
/// </summary>
/// <remarks>
/// Works against any <see cref="IDirectionalResolutionSource"/>, so a composed entity
/// (VisualComposer) and a simple one (a bare AnimationOrchestrator driving a single
/// AnimatedSprite3DComponent) wire identically. Pure-vertical facings (X ~ 0) leave the
/// current mirror untouched, so side-view art keeps its last horizontal facing while
/// moving straight up/down. Application is per-animator: only the resolving animator's
/// sprites are touched, letting an 8-directional body coexist with single-direction
/// hands mid-migration.
/// </remarks>
[GlobalClass, Tool]
public partial class FacingFlipController : Node
{
    /// <summary>
    /// The node announcing clip resolution — a VisualComposer, an AnimationOrchestrator,
    /// or a CompositeAnimatorComponent. A composer additionally scopes flipping to the
    /// resolving animator's slot, so every visual node in that slot mirrors together.
    /// </summary>
    [Export, RequiredExport] public Node AnimationSource { get; set; } = null!;

    /// <summary>
    /// The direction the source art faces when unflipped. FlipH is applied
    /// whenever the current facing opposes this.
    /// </summary>
    [Export] public bool ArtFacesRight { get; set; } = true;

    private IDirectionalResolutionSource? _source;
    private VisualComposer? _composer;
    private bool _warnedUnflippableAnimator;
    private bool _warnedUnusableSource;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint()) { return; }

        ResolveSource();
        if (_source == null)
        {
            // An UNSET export is _Ready's ValidateRequiredExports to report. A node that is
            // assigned but announces nothing would otherwise leave the controller permanently
            // inert with no runtime trace — the editor warning only reaches an author who has
            // the scene open. Latched because _EnterTree re-runs on every reparent.
            if (AnimationSource != null && !_warnedUnusableSource)
            {
                _warnedUnusableSource = true;
                JmoLogger.Error(this,
                    $"AnimationSource '{AnimationSource.Name}' announces no clip resolution — expected a "
                    + "VisualComposer, an AnimationOrchestrator, or a CompositeAnimatorComponent. "
                    + "Facing flipping is disabled for this entity.");
            }
            return;
        }

        // Idempotent, and paired with _ExitTree rather than _Ready: _ExitTree fires on reparent
        // but _Ready runs once per node lifetime, so a _Ready-bound subscription dies silently
        // the first time a visual is reparented.
        _source.DirectionalResolutionApplied -= OnResolutionApplied;
        _source.DirectionalResolutionApplied += OnResolutionApplied;
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) { return; }
        this.ValidateRequiredExports();
    }

    public override void _ExitTree()
    {
        if (_source != null)
        {
            _source.DirectionalResolutionApplied -= OnResolutionApplied;
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (AnimationSource == null)
        {
            warnings.Add("AnimationSource is unset. Assign a VisualComposer, an AnimationOrchestrator, or a CompositeAnimatorComponent.");
        }
        else if (AnimationSource is not VisualComposer && AnimationSource is not IDirectionalResolutionSource)
        {
            warnings.Add($"AnimationSource '{AnimationSource.Name}' does not announce clip resolution. Assign a VisualComposer, an AnimationOrchestrator, or a CompositeAnimatorComponent.");
        }

        if (AnimationSource is AnimationOrchestrator { DirectionSet: null })
        {
            warnings.Add("The assigned AnimationOrchestrator has no DirectionSet, so its facing is always Vector3.Zero and every resolution reads as pure-vertical — no flip will ever apply. Assign a DirectionSet3D on the orchestrator.");
        }

        return warnings.ToArray();
    }

    private void ResolveSource()
    {
        _composer = AnimationSource as VisualComposer;
        _source = _composer != null
            ? _composer.CompositeAnimator
            : AnimationSource as IDirectionalResolutionSource;
    }

    private void OnResolutionApplied(IAnimComponent animator, StringName? resolvedName, DirectionalAnimRequest request)
    {
        if (resolvedName == null) { return; }

        if (resolvedName != request.BaseName)
        {
            // Directional art is facing-correct — clear any mirror left over from a
            // previously-playing base clip, or the directional frames render reversed.
            ApplyFlip(animator, false);
            return;
        }

        // Shared mirror convention (single source in JmoMath): null == pure-vertical → hold current.
        var mirror = JmoMath.ShouldMirrorHorizontal(request.Direction.X, ArtFacesRight);
        if (mirror == null) { return; }

        ApplyFlip(animator, mirror.Value);
    }

    private void ApplyFlip(IAnimComponent animator, bool flip)
    {
        if (_composer != null && TryFlipComposerSlot(animator, flip)) { return; }

        // The capability must sit on the animator itself (AnimatedSprite3DComponent is both),
        // not on a host — a sibling-sprite walk would silently mirror unintended nodes.
        if (animator is ISpriteComponent sprite)
        {
            sprite.FlipH = flip;
            return;
        }

        WarnUnflippableAnimatorOnce(animator);
    }

    private bool TryFlipComposerSlot(IAnimComponent animator, bool flip)
    {
        foreach (var slot in _composer!.Slots)
        {
            if (!ReferenceEquals(slot.Animator, animator)) { continue; }

            foreach (var handle in slot.GetVisualNodes(VisualQuery.All))
            {
                // SpriteBase3D, not Spr3D: AnimatedSprite3D is a sibling type, not a subclass.
                if (handle.Node is SpriteBase3D sprite)
                {
                    sprite.FlipH = flip;
                }
            }
            return true;
        }
        return false;
    }

    private void WarnUnflippableAnimatorOnce(IAnimComponent animator)
    {
        if (_warnedUnflippableAnimator) { return; }
        _warnedUnflippableAnimator = true;

        // IAnimComponent is not constrained to Node, and GetUnderlyingNode() may return null on a
        // non-Node implementation — neither may throw from the branch whose only job is to report.
        var animatorName = (animator as Node)?.Name.ToString() ?? animator.GetType().Name;

        JmoLogger.Warning(this,
            $"Animator '{animatorName}' is not an ISpriteComponent and owns no VisualComposer slot, "
            + "so there is no sprite to mirror. Target an AnimatedSprite3DComponent (or wire a VisualComposer) "
            + "— flipping is a no-op for this animator.");
    }
}
