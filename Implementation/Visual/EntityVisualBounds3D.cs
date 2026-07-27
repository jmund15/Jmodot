namespace Jmodot.Implementation.Visual;

using Godot;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Core.Visual.Sprite;
using Jmodot.Implementation.Components;
using Jmodot.Implementation.Visual.Animation.Sprite;

/// <summary>
/// Measured silhouette of an entity's live art. <paramref name="Height"/> is world-space;
/// <paramref name="CenterOffsetY"/> is the measured sprite's origin expressed in the entity's own
/// local space, so an attached visual can centre on the body without an authored magic offset.
/// </summary>
/// <param name="Height">World-space height of the entity's tallest live sprite.</param>
/// <param name="CenterOffsetY">
/// The measured sprite's origin in entity-local space. Exact for the default
/// <c>Centered = true, Offset = (0,0)</c> sprite authoring; non-centered or pixel-offset sprites are
/// NOT compensated, because guessing the sign conventions untested is worse than a stated limit.
/// </param>
public readonly record struct VisualBounds3D(float Height, float CenterOffsetY)
{
    /// <summary>Sentinel for an entity whose live art could not be measured.</summary>
    public static readonly VisualBounds3D Unmeasured = new(0f, 0f);

    public bool IsMeasured => Height > 0f;
}

/// <summary>
/// Measures the silhouette of a 3D entity's live sprite art, so attached visuals (status
/// encasements, auras, shields) can size and centre themselves on the body they decorate.
/// </summary>
public static class EntityVisualBounds3D
{
    /// <summary>
    /// Silhouette of <paramref name="entity"/>'s live art, optionally ignoring <paramref name="exclude"/>
    /// and everything beneath it so a caller can leave its own art out of the measurement.
    /// Returns <see cref="VisualBounds3D.Unmeasured"/> when nothing live can be measured.
    /// </summary>
    public static VisualBounds3D Measure(Node3D entity, Node? exclude = null)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity)) { return VisualBounds3D.Unmeasured; }

        VisualBounds3D bounds = MeasureSilhouette(entity, exclude);
        if (!bounds.IsMeasured) { return VisualBounds3D.Unmeasured; }

        // Folded into Height only: the controller's VisualRoot is an ANCESTOR of the sprite, so the
        // sprite's own local scale never carries the factor — but CenterOffsetY comes from live global
        // positions and therefore already reflects it.
        return bounds with { Height = bounds.Height * SizeFactor(entity) };
    }

    private static VisualBounds3D MeasureSilhouette(Node3D entity, Node? exclude)
    {
        if (entity.TryGetFirstChildOfType<VisualComposer>(out var composer, includeSubChildren: true)
            && composer != null)
        {
            return MeasureComposed(entity, composer);
        }

        return MeasureTreeWalk(entity, exclude);
    }

    /// <summary>
    /// Silhouette of a composer-driven entity, taken from the Master slot — the body. Sizing off a held
    /// item or a hat would be wrong.
    /// </summary>
    /// <remarks>
    /// A composer that reports nothing visible returns Unmeasured rather than falling through to the tree
    /// walk: a composer keeps every animation of every slot resident, and those frames span art regimes,
    /// so the walk would silently measure hidden art from a different regime.
    /// </remarks>
    private static VisualBounds3D MeasureComposed(Node3D entity, VisualComposer composer)
    {
        float tallest = 0f;
        float offsetY = 0f;

        // GetVisibleNodes, NOT GetVisualNodes(VisualQuery.VisibleOnly) — the latter matches the handle's
        // build-time visibility snapshot, which is false for every slot an AnimationVisibilityCoordinator
        // drives, so it silently returns an empty set with no error.
        foreach (var handle in composer.GetVisibleNodes(MasterSlotQuery(composer)))
        {
            if (handle.Node is not SpriteBase3D sprite || !GodotObject.IsInstanceValid(sprite)) { continue; }

            float height = RenderedHeight(sprite);
            if (height <= tallest) { continue; }

            tallest = height;
            offsetY = entity.ToLocal(sprite.GlobalPosition).Y;
        }

        return tallest > 0f ? new VisualBounds3D(tallest, offsetY) : VisualBounds3D.Unmeasured;
    }

    private static VisualQuery MasterSlotQuery(VisualComposer composer)
    {
        foreach (var slot in composer.Slots)
        {
            if (slot == null || slot.SyncMode != AnimationSyncMode.Master || slot.Key == null) { continue; }

            return VisualQuery.Slot(slot.Key);
        }

        return VisualQuery.All;
    }

    /// <summary>
    /// Silhouette of a single-sprite entity (NPC, prop). Falling back to a hidden sprite is safe here:
    /// without a composer there is no art-regime ambiguity to get wrong.
    /// </summary>
    private static VisualBounds3D MeasureTreeWalk(Node3D entity, Node? exclude)
    {
        var tallest = VisualBounds3D.Unmeasured;
        var tallestVisible = VisualBounds3D.Unmeasured;

        foreach (var sprite in entity.GetChildrenOfType<SpriteBase3D>(includeSubChildren: true))
        {
            if (!GodotObject.IsInstanceValid(sprite)) { continue; }
            if (exclude != null && (ReferenceEquals(sprite, exclude) || exclude.IsAncestorOf(sprite))) { continue; }

            var candidate = new VisualBounds3D(RenderedHeight(sprite), entity.ToLocal(sprite.GlobalPosition).Y);
            if (candidate.Height > tallest.Height) { tallest = candidate; }
            if (sprite.IsVisibleInTree() && candidate.Height > tallestVisible.Height) { tallestVisible = candidate; }
        }

        return tallestVisible.IsMeasured ? tallestVisible : tallest;
    }

    /// <summary>
    /// AppliedScale, not RuntimeScaleMultiplier: the latter is pre-clamp and drops the stat-driven base
    /// size. The controller is genuinely optional, hence TryGet rather than the throwing lookup.
    /// </summary>
    private static float SizeFactor(Node3D entity)
    {
        if (entity.TryGetFirstChildOfType<EntitySizeController>(out var sizeController, includeSubChildren: true)
            && sizeController != null)
        {
            return sizeController.AppliedScale;
        }

        return 1f;
    }

    /// <summary>World height of the sprite as it currently renders. Defers to the framework sprite contract when the node implements it.</summary>
    private static float RenderedHeight(SpriteBase3D sprite)
        => sprite is ISpriteComponent component ? component.GetSpriteHeight() : FrameHeight(sprite) * sprite.Scale.Y;

    /// <summary>World height of one animation frame at a node scale of 1.</summary>
    private static float FrameHeight(SpriteBase3D sprite)
    {
        return sprite switch
        {
            AnimatedSprite3D animated =>
                (animated.SpriteFrames?.GetFrameTexture(animated.Animation, animated.Frame)?.GetHeight() ?? 0)
                * animated.PixelSize,
            Sprite3D still =>
                (still.Texture?.GetHeight() ?? 0) / (float)Mathf.Max(1, still.Vframes) * still.PixelSize,
            _ => 0f,
        };
    }
}
