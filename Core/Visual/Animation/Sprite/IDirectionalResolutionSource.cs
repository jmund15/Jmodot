namespace Jmodot.Core.Visual.Animation.Sprite;

using System;
using Godot;

/// <summary>
/// Announces which concrete clip a <see cref="DirectionalAnimRequest"/> resolved to, per animator.
/// Both resolution paths run the same <c>DirectionalClipResolver</c> — the composite fans out
/// per-slave, a leaf orchestrator resolves once — so this contract lets a consumer observe the
/// outcome without knowing which topology produced it.
/// </summary>
/// <remarks>
/// Deliberately NOT folded into <see cref="IAnimationOrchestrator"/>: announcing resolution varies
/// on an axis orthogonal to driving named animations, and an orchestrator implementation may
/// legitimately not be a source.
/// </remarks>
public interface IDirectionalResolutionSource
{
    /// <summary>
    /// Fired once per animator per resolution, including late-registration catch-up.
    /// <c>resolvedName</c> is the clip the animator was told to play, or null when nothing
    /// resolved (the animator was stopped/hidden). Comparing it against
    /// <see cref="DirectionalAnimRequest.BaseName"/> distinguishes undirected base art
    /// (single-direction, facing-flippable) from directional art (already facing-correct).
    /// </summary>
    event Action<IAnimComponent, StringName?, DirectionalAnimRequest> DirectionalResolutionApplied;
}
