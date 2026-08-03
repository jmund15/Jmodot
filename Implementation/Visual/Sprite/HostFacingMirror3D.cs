namespace Jmodot.Implementation.Visual.Sprite;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Components;
using Jmodot.Core.Interaction;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Interaction.Attachment;
using Jmodot.Implementation.Visual.Animation.Sprite;
using Shared;
using GCol = Godot.Collections;

/// <summary>
/// Mirrors a rider's attach art to the facing of whatever host it is riding, for the whole ride and no
/// longer. Authored under the RIDER's visuals: the art being mirrored is the rider's, and the host needs
/// no knowledge that anything is watching it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Facing is read from clip RESOLUTION, not from a flip flag.</b> No host exposes a facing scalar —
/// a directional-clip host carries facing in which variant it selected, and
/// <see cref="FacingFlipController"/>'s own flip state is per-composer-slot. So this derives from the
/// <see cref="DirectionalAnimRequest.Direction"/> the host last resolved against, which every host
/// topology reports through <see cref="IDirectionalResolutionSource"/> — directional-clip hosts and
/// FlipH hosts alike.
/// </para>
/// <para>
/// <b>Event observation alone is stale by default</b>, which is why the attach path also reads
/// <see cref="IDirectionalResolutionSource.TryGetLastResolution"/>: a host standing still emits no
/// resolution, so roughly half of all attaches would render un-mirrored until the host next moved.
/// </para>
/// <para>Required BB key: <see cref="BBDataSig.AttachmentRider"/> — with no rider there is no ride to
/// mirror, so its absence fails initialization rather than leaving a silent node in the scene.</para>
/// </remarks>
[GlobalClass, Tool]
public partial class HostFacingMirror3D : Node, IComponent
{
    /// <summary>
    /// The rider's attach-art sprites to mirror. An explicit list, never a subtree walk: the rider's
    /// visuals also hold its locomotion art, which has its own facing rules.
    /// </summary>
    // A field, not a property: node-reference exports on properties deserialize empty when a scene is
    // instantiated at runtime, and every rider that uses this is a spawned entity.
    [Export] public GCol.Array<Node> Sprites = new();

    /// <summary>The direction the rider's attach art faces unflipped. FlipH applies whenever the host's facing opposes this.</summary>
    [Export] public bool ArtFacesRight { get; set; } = true;

    private readonly List<SpriteBase3D> _resolved = new();
    private AttachmentRiderComponent3D? _rider;
    private IDirectionalResolutionSource? _hostSource;
    private bool _warnedNoHostSource;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (this.Sprites.Count == 0)
        {
            warnings.Add("Sprites is empty, so this node mirrors nothing. Assign the rider's attach-art sprites.");
        }

        for (var i = 0; i < this.Sprites.Count; i++)
        {
            if (this.Sprites[i] == null) { warnings.Add($"Sprites[{i}] is empty."); }
            else if (this.Sprites[i] is not SpriteBase3D)
            {
                warnings.Add($"Sprites[{i}] ('{this.Sprites[i].Name}') is not a SpriteBase3D, so it has nothing to mirror.");
            }
        }

        if (this.GetParent() != null && !this.HasRiderInEntity())
        {
            warnings.Add("No AttachmentRiderComponent3D on this entity. Host-facing mirroring only means "
                + "anything for a rider, so this node would never do anything here.");
        }

        return warnings.ToArray();
    }

    /// <remarks>
    /// Scoped by <c>Owner</c> — the entity scene's root for anything authored in it — rather than by a
    /// fixed number of hops up: the rider component lives wherever that entity groups its combat
    /// components, which is a subtree away from the visuals this node sits in.
    /// </remarks>
    private bool HasRiderInEntity()
    {
        var entity = this.Owner ?? this.GetParent();
        return entity != null && entity.TryGetFirstChildOfType<AttachmentRiderComponent3D>(out _, true);
    }

    #region IComponent

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    /// <summary>
    /// An empty or holey sprite list is an authoring mistake, not a missing capability, so it earns an
    /// Error of its own here on top of the configuration warning — the two rungs cover the designer who
    /// never opens the scene dock and the one who never reads the log.
    /// </summary>
    public bool Initialize(IBlackboard bb)
    {
        this.Unsubscribe();

        if (!this.ValidateSprites()) { return false; }

        if (!bb.TryGet<AttachmentRiderComponent3D>(BBDataSig.AttachmentRider, out var rider) || rider == null)
        {
            JmoLogger.Debug(this, "[Attachment] No AttachmentRiderComponent3D on the blackboard — nothing to mirror.");
            return false;
        }

        this._rider = rider;
        IsInitialized = true;
        Initialized();
        return true;
    }

    private bool ValidateSprites()
    {
        this._resolved.Clear();

        if (this.Sprites.Count == 0)
        {
            JmoLogger.Error(this, "[Attachment] HostFacingMirror3D has no Sprites assigned, so no attach art "
                + "would ever mirror to the host's facing.");
            return false;
        }

        foreach (var entry in this.Sprites)
        {
            if (entry is SpriteBase3D sprite)
            {
                this._resolved.Add(sprite);
                continue;
            }

            JmoLogger.Error(this, "[Attachment] HostFacingMirror3D holds a Sprites entry that is empty or not a "
                + "SpriteBase3D. Fix every entry — one bad slot means one pose silently stops mirroring.");
            this._resolved.Clear();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Phase 2, and with catch-up: the rider is a sibling component, so its events are only safe here —
    /// and an entity re-initialized while already riding (pool reuse, scene rebind) would otherwise wait
    /// for a second attach that never comes.
    /// </summary>
    public void OnPostInitialize()
    {
        if (this._rider == null) { return; }

        this._rider.AttachmentStarted -= this.OnAttachmentStarted;
        this._rider.AttachmentStarted += this.OnAttachmentStarted;
        this._rider.AttachmentEnded -= this.OnAttachmentEnded;
        this._rider.AttachmentEnded += this.OnAttachmentEnded;

        if (this._rider is { IsAttached: true, Host: not null })
        {
            this.OnAttachmentStarted(this._rider.Host);
        }
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    private void OnAttachmentStarted(IAttachmentHost host)
    {
        this.Unsubscribe();

        this._hostSource = ResolveHostSource(host.HostEntity);
        if (this._hostSource == null)
        {
            this.WarnNoHostSourceOnce(host);
            return;
        }

        this._hostSource.DirectionalResolutionApplied += this.OnResolutionApplied;

        if (this._hostSource.TryGetLastResolution(out var request)) { this.ApplyMirror(request); }
    }

    private void OnAttachmentEnded(DetachCause cause)
    {
        this.Unsubscribe();
        this.ApplyFlip(false);
    }

    /// <summary>
    /// Deterministic by construction, mirroring <see cref="FacingFlipController"/>'s own resolution: a
    /// composed host carries TWO resolution sources, so first-child-of-interface would answer whichever
    /// the scene happened to list first. The composer's composite animator is the one that actually
    /// resolves the host's art; a <c>DirectionSet</c>-less orchestrator is skipped because it reports a
    /// zero facing forever, which reads as pure-vertical and would silently never mirror.
    /// </summary>
    private static IDirectionalResolutionSource? ResolveHostSource(Node3D hostEntity)
    {
        if (hostEntity.TryGetFirstChildOfType<VisualComposer>(out var composer, true)
            && composer?.CompositeAnimator != null)
        {
            return composer.CompositeAnimator;
        }

        foreach (var source in hostEntity.GetChildrenOfInterface<IDirectionalResolutionSource>(true))
        {
            if (source is AnimationOrchestrator { DirectionSet: null }) { continue; }

            return source;
        }

        return null;
    }

    /// <remarks>
    /// The animator and resolved clip name are deliberately unread: the rider's attach art is
    /// single-direction by construction, so unlike <see cref="FacingFlipController"/> there is no
    /// already-facing-correct case to clear, and every resolution the host fans out carries the same
    /// facing.
    /// </remarks>
    private void OnResolutionApplied(IAnimComponent animator, StringName? resolvedName, DirectionalAnimRequest request)
    {
        this.ApplyMirror(request);
    }

    private void ApplyMirror(DirectionalAnimRequest request)
    {
        // Shared mirror convention (single source in JmoMath): null == pure-vertical → hold current, so a
        // host walking straight up-screen keeps the last horizontal facing rather than snapping.
        var mirror = JmoMath.ShouldMirrorHorizontal(request.Direction.X, this.ArtFacesRight);
        if (mirror == null) { return; }

        this.ApplyFlip(mirror.Value);
    }

    private void ApplyFlip(bool flip)
    {
        foreach (var sprite in this._resolved)
        {
            if (!GodotObject.IsInstanceValid(sprite)) { continue; }

            sprite.FlipH = flip;
        }
    }

    private void Unsubscribe()
    {
        if (this._hostSource == null) { return; }

        this._hostSource.DirectionalResolutionApplied -= this.OnResolutionApplied;
        this._hostSource = null;
    }

    private void WarnNoHostSourceOnce(IAttachmentHost host)
    {
        if (this._warnedNoHostSource) { return; }
        this._warnedNoHostSource = true;

        JmoLogger.Warning(this, $"[Attachment] Host '{host.HostEntity.Name}' announces no usable clip "
            + "resolution, so this rider's attach art cannot follow its facing. Give the host a "
            + "VisualComposer, or an AnimationOrchestrator with a DirectionSet3D.");
    }
}
