namespace Jmodot.Implementation.Visual.Sprite;

using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
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
/// Successor to the legacy VisualComposer FlipH debug toggle (removed in the
/// typed-slot redesign); presence of this node beside the composer enables the
/// behavior.
/// </summary>
/// <remarks>
/// Pure-vertical facings (X ~ 0) leave the current mirror untouched, so side-view
/// art keeps its last horizontal facing while moving straight up/down. Application
/// is per-slot: only the resolving animator's slot sprites are touched, letting an
/// 8-directional body coexist with single-direction hands mid-migration.
/// </remarks>
[GlobalClass]
public partial class FacingFlipController : Node
{
    [Export, RequiredExport] public VisualComposer Composer { get; set; } = null!;

    /// <summary>
    /// The direction the source art faces when unflipped. FlipH is applied
    /// whenever the current facing opposes this.
    /// </summary>
    [Export] public bool ArtFacesRight { get; set; } = true;

    private CompositeAnimatorComponent _composite = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        _composite = Composer.CompositeAnimator;
        _composite.DirectionalResolutionApplied += OnResolutionApplied;
    }

    public override void _ExitTree()
    {
        if (_composite != null)
        {
            _composite.DirectionalResolutionApplied -= OnResolutionApplied;
        }
    }

    private void OnResolutionApplied(IAnimComponent animator, StringName? resolvedName, DirectionalAnimRequest request)
    {
        if (resolvedName == null) { return; }

        bool isBaseClip = resolvedName == request.BaseName;
        if (isBaseClip)
        {
            // Shared mirror convention (single source in JmoMath): null == pure-vertical → hold current.
            var mirror = JmoMath.ShouldMirrorHorizontal(request.Direction.X, ArtFacesRight);
            if (mirror == null) { return; }
            ApplyToAnimatorSlot(animator, mirror.Value);
            return;
        }

        // Directional art is facing-correct — clear any mirror left over from a
        // previously-playing base clip, or the directional frames render reversed.
        ApplyToAnimatorSlot(animator, false);
    }

    private void ApplyToAnimatorSlot(IAnimComponent animator, bool flip)
    {
        foreach (var slot in Composer.Slots)
        {
            if (!ReferenceEquals(slot.Animator, animator)) { continue; }

            foreach (var handle in slot.GetVisualNodes(VisualQuery.All))
            {
                if (handle.Node is SpriteBase3D sprite)
                {
                    sprite.FlipH = flip;
                }
            }
            return;
        }
    }
}
