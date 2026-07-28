namespace Jmodot.Implementation.Visual;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Visual;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Implementation.Visual.Animation.Sprite;

/// <summary>
/// Measured silhouette of an entity's live art: the union of its live sprite extents, and that
/// union's centre, both in the entity's own local space — so an attached visual can size and centre
/// itself on the body without an authored magic offset.
/// </summary>
/// <param name="Size">
/// Extents the art READS as, not its world-space projection. Sprite planes hang below an iso tilt
/// that foreshortens them, and an attached overlay must match the apparent silhouette.
/// <see cref="Depth"/> is zero for the planar sprite art shipped today; it becomes meaningful the
/// moment non-planar art is measured, and <see cref="Largest"/> picks it up with no change here.
/// </param>
/// <param name="Center">Centre of that union, in entity-local space.</param>
public readonly record struct VisualBounds3D(Vector3 Size, Vector3 Center)
{
    /// <summary>Sentinel for an entity whose live art could not be measured.</summary>
    public static readonly VisualBounds3D Unmeasured = new(Vector3.Zero, Vector3.Zero);

    public bool IsMeasured => Size.Y > 0f || Size.X > 0f;

    /// <summary>Horizontal extent of the silhouette.</summary>
    public float Width => Size.X;

    /// <summary>Vertical extent of the silhouette.</summary>
    public float Height => Size.Y;

    /// <summary>Extent along the view axis. Always zero while all measured art is planar.</summary>
    public float Depth => Size.Z;

    /// <summary>
    /// The dominant extent — what a shell must match to cover the body regardless of which way the
    /// entity is proportioned. Written over all three components deliberately: today Depth is zero
    /// and contributes nothing, and when it stops being zero this needs no edit.
    /// </summary>
    public float Largest => Mathf.Max(Size.X, Mathf.Max(Size.Y, Size.Z));

    /// <summary>Height of the silhouette's centre above the entity origin.</summary>
    public float CenterOffsetY => Center.Y;
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

        // Node3D.GlobalTransform ERR_FAILs to identity outside the tree, which would silently
        // collapse every sprite onto the entity origin rather than reporting nothing measurable.
        if (!entity.IsInsideTree()) { return VisualBounds3D.Unmeasured; }

        if (entity.TryGetFirstChildOfType<VisualComposer>(out var composer, includeSubChildren: true)
            && composer != null)
        {
            return Union(entity, ComposedSprites(composer, exclude));
        }

        return MeasureTreeWalk(entity, exclude);
    }

    /// <summary>
    /// Vertical union of the sprites' art, expressed in <paramref name="entity"/>'s local space.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT built from <c>VisualInstance3D.GetAabb</c>, which looks like the obvious
    /// primitive and is wrong twice over here: it reports an empty box for a sprite that has not
    /// drawn yet, and a sprite authored with <c>Axis = Y</c> lies flat in XZ, so the box's Y extent
    /// is legitimately zero rather than the art's height. Height therefore comes from the frame
    /// configuration, which is orientation-independent, and only the SCALE of the transform chain
    /// is applied to it.
    /// </remarks>
    private static VisualBounds3D Union(Node3D entity, IEnumerable<SpriteBase3D> sprites)
    {
        Transform3D toEntity = entity.GlobalTransform.AffineInverse();
        var low = new Vector2(float.MaxValue, float.MaxValue);
        var high = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        foreach (var sprite in sprites)
        {
            if (!GodotObject.IsInstanceValid(sprite)) { continue; }

            Vector2 art = ArtSize(sprite);
            if (art.Y <= 0f && art.X <= 0f) { continue; }

            Transform3D relative = toEntity * sprite.GlobalTransform;
            // Scale without rotation: ancestor scale (an EntitySizeController writes it onto the
            // visual root) reaches the sprite through this chain and is counted exactly once here,
            // while the iso tilt that would foreshorten the art is correctly ignored.
            Vector3 scale = relative.Basis.Scale;
            var extent = new Vector2(art.X * scale.X, art.Y * scale.Y);
            Vector2 centre = new Vector2(relative.Origin.X, relative.Origin.Y)
                + (ArtCentreOffset(sprite, art) * new Vector2(scale.X, scale.Y));

            low = low.Min(centre - (extent * 0.5f));
            high = high.Max(centre + (extent * 0.5f));
            any = true;
        }

        if (!any) { return VisualBounds3D.Unmeasured; }

        Vector2 size = high - low;
        Vector2 middle = (high + low) * 0.5f;

        // Depth stays zero: every sprite measured here is planar, and inventing a value would make
        // Largest claim an extent nothing rendered.
        return new VisualBounds3D(new Vector3(size.X, size.Y, 0f), new Vector3(middle.X, middle.Y, 0f));
    }

    /// <summary>
    /// Offset from the sprite's node origin to the centre of its rendered art, at a scale of 1.
    /// Zero for the default authoring, which is why ignoring it goes unnoticed until the first
    /// sprite that sets either property — then every attached visual slides off the body at once.
    /// </summary>
    /// <remarks>
    /// Pinned against <c>VisualInstance3D.GetAabb</c> for a drawn <c>Axis = Z</c> sprite, which is
    /// the only configuration where the engine reports a usable box and can therefore serve as
    /// ground truth for these placement rules.
    /// </remarks>
    private static Vector2 ArtCentreOffset(SpriteBase3D sprite, Vector2 artSize)
    {
        // Offset is in texture pixels, with +Y up in 3D (unlike Sprite2D).
        Vector2 offset = sprite.Offset * sprite.PixelSize;
        if (sprite.Centered) { return offset; }

        // A non-centred quad is anchored at its corner rather than its middle, so the art's centre
        // sits half a frame away along both axes.
        return offset + (artSize * 0.5f);
    }

    /// <summary>
    /// Live sprites of a composer-driven entity, taken from the Master slot — the body. Sizing off a
    /// held item or a hat would be wrong.
    /// </summary>
    /// <remarks>
    /// A composer that reports nothing visible yields nothing rather than falling through to the
    /// tree walk: a composer keeps every animation of every slot resident, and those frames span art
    /// regimes, so the walk would silently measure hidden art from a different regime.
    /// </remarks>
    private static IEnumerable<SpriteBase3D> ComposedSprites(VisualComposer composer, Node? exclude)
    {
        // GetVisibleNodes, NOT GetVisualNodes(VisualQuery.VisibleOnly) — the latter matches the
        // handle's build-time visibility snapshot, which is false for every slot an
        // AnimationVisibilityCoordinator drives, so it silently returns an empty set with no error.
        foreach (var handle in composer.GetVisibleNodes(MasterSlotQuery(composer)))
        {
            if (handle.Node is not SpriteBase3D sprite) { continue; }
            // Honoured on BOTH branches or the parameter is a lie: a caller that registers its own
            // art in a composer slot would otherwise measure itself and grow without bound.
            if (exclude != null && (ReferenceEquals(sprite, exclude) || exclude.IsAncestorOf(sprite))) { continue; }

            yield return sprite;
        }
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
    /// Silhouette of a single-sprite entity (NPC, prop). Falling back to hidden sprites is safe
    /// here: without a composer there is no art-regime ambiguity to get wrong.
    /// </summary>
    private static VisualBounds3D MeasureTreeWalk(Node3D entity, Node? exclude)
    {
        var all = new List<SpriteBase3D>();
        var visible = new List<SpriteBase3D>();

        foreach (var sprite in entity.GetChildrenOfType<SpriteBase3D>(includeSubChildren: true))
        {
            if (!GodotObject.IsInstanceValid(sprite)) { continue; }
            if (exclude != null && (ReferenceEquals(sprite, exclude) || exclude.IsAncestorOf(sprite))) { continue; }

            all.Add(sprite);
            if (sprite.IsVisibleInTree()) { visible.Add(sprite); }
        }

        VisualBounds3D shown = Union(entity, visible);

        return shown.IsMeasured ? shown : Union(entity, all);
    }

    /// <summary>
    /// Size of one frame of the sprite's art at a node scale of 1. Public because an overlay
    /// fitting itself to a measured entity must divide out its OWN authored art size using the same
    /// rule the measurement used — deriving the two differently is how a scene's pixel_size silently
    /// changes a rendered size.
    /// </summary>
    /// <remarks>
    /// Read from the frame configuration, never from the quad's geometry, so the answer does not
    /// depend on which local axis the art happens to face. An <c>Axis = Y</c> sprite lies flat in
    /// XZ; its art is still as wide and tall as its texture says.
    /// </remarks>
    public static Vector2 ArtSize(SpriteBase3D sprite)
    {
        return sprite switch
        {
            AnimatedSprite3D animated => FrameSize(
                animated.SpriteFrames?.GetFrameTexture(animated.Animation, animated.Frame),
                hframes: 1,
                vframes: 1) * animated.PixelSize,
            Sprite3D still => (still.RegionEnabled
                ? still.RegionRect.Size
                : FrameSize(still.Texture, still.Hframes, still.Vframes)) * still.PixelSize,
            _ => Vector2.Zero,
        };
    }

    private static Vector2 FrameSize(Texture2D? texture, int hframes, int vframes)
    {
        if (texture == null) { return Vector2.Zero; }

        return new Vector2(
            texture.GetWidth() / (float)Mathf.Max(1, hframes),
            texture.GetHeight() / (float)Mathf.Max(1, vframes));
    }
}
