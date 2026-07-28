namespace Jmodot.Implementation.Visual;

using Godot;
using Jmodot.Core.Visual;
using Jmodot.Implementation.Shared;

/// <summary>
/// Which measured extent a shell sizes itself against.
/// </summary>
/// <remarks>
/// There is deliberately no Depth member. Every sprite measured today is planar, so it would be a
/// selectable Inspector option that leaves the art silently unscaled.
/// <see cref="VisualBounds3D.Largest"/> already folds the depth extent in the moment non-planar art
/// is measured, so adding the member then is a one-line change with real behaviour behind it.
/// </remarks>
public enum ShellFitAxis
{
    /// <summary>The body's dominant extent — covers it whichever way it is proportioned.</summary>
    Largest = 0,

    /// <summary>Always the vertical extent, letting a wide body overflow the shell sideways.</summary>
    Height = 1,

    /// <summary>Always the horizontal extent, letting a tall body overflow the shell vertically.</summary>
    Width = 2,
}

/// <summary>
/// Art that encases the entity it is parented to — a freeze block, a shield bubble, a stone
/// casing, a web cocoon. Measures the body once, sizes its own art in units of that body's extent,
/// and keeps it centred on the silhouette. The ratio driving the size is pushed in by whatever owns
/// the depleting resource; this node never subscribes to anything.
/// </summary>
/// <remarks>
/// Sizing and centring are deliberately split. Centring depends only on the measured body and is
/// applied ONCE; sizing depends on a ratio that changes every frame. Folding them together forces
/// the stable half to re-run at the volatile half's cadence, and — worse — invites a per-frame
/// re-measure of a body that has not moved.
/// <para>
/// 2D parity (per the framework's 2D/3D convention) is deferred with the measurement seam it
/// depends on: <see cref="EntityVisualBounds3D"/> has no 2D counterpart until Jmodot gains a 2D
/// composition surface.
/// </para>
/// </remarks>
[GlobalClass]
public partial class EntityShellOverlay3D : Node3D
{
    /// <summary>
    /// Maps the driving ratio to a shell size in multiples of the measured body extent. Absent
    /// leaves the art at one body extent rather than collapsing it — an unconfigured shell should
    /// look wrong, not invisible.
    /// </summary>
    [Export] public ScaleRatioProfile? SizeProfile { get; set; }

    /// <summary>
    /// Which extent of the body the shell matches, and — the same selector on both sides — which
    /// extent of its own art it divides out. Defaults to the dominant one so a 1.0 x 1.5m body
    /// sizes the shell off 1.5m without the shell needing to know how the body is proportioned.
    /// </summary>
    [Export] public ShellFitAxis FitAxis { get; set; } = ShellFitAxis.Largest;

    /// <summary>The encasing art. First sprite below this node; subclasses may need it for end-of-life FX.</summary>
    protected SpriteBase3D? ShellArt { get; private set; }

    /// <summary>Silhouette of the parent entity, measured once at <see cref="_Ready"/>.</summary>
    protected VisualBounds3D Bounds { get; private set; } = VisualBounds3D.Unmeasured;

    private Vector2 _artSizeAtScale1;

    public override void _Ready()
    {
        base._Ready();

        if (this.TryGetFirstChildOfType<SpriteBase3D>(out var art) && art != null)
        {
            ShellArt = art;
            // Sampled before any scale is written, so it is the art's size at a node scale of 1.
            _artSizeAtScale1 = EntityVisualBounds3D.ArtSize(art);
        }

        // Measured once rather than per frame: the walk is a hot-path cost, and a composer-driven
        // entity would pop between art regimes as its animation changed mid-effect.
        Bounds = ResolveBody() is Node3D body
            ? EntityVisualBounds3D.Measure(body, exclude: this)
            : VisualBounds3D.Unmeasured;

        // An unmeasurable body is not recoverable here, and the fallback renders the art at its raw
        // authored size — plausible enough to pass a playtest while being wrong on every entity.
        // Say so rather than letting a mis-parented shell look merely mis-tuned.
        if (!Bounds.IsMeasured)
        {
            JmoLogger.Warning(this,
                $"[Visual] {Name} could not measure a body to encase; its art renders at the authored size.");
        }

        ApplyCenter();
    }

    /// <summary>
    /// The entity this shell encases. Defaults to the direct parent, which is where a status
    /// runner's PersistentVisuals land. Override when the shell is authored deeper in a visual
    /// subtree, where the parent is a slot or pivot rather than the body root.
    /// </summary>
    protected virtual Node3D? ResolveBody() => GetParent() as Node3D;

    /// <summary>
    /// Resizes the shell for a 0..1 ratio of whatever resource drives it. Centring is untouched —
    /// a shrinking shell contracts toward the body it encases, it does not slide down it.
    /// </summary>
    public void SetRatio(float ratio)
    {
        if (ShellArt == null || !GodotObject.IsInstanceValid(ShellArt)) { return; }

        float sizeUnits = SizeProfile?.Evaluate(ratio) ?? 1f;

        // Scale the art, not this root: a node's scale multiplies its children and its own art but
        // never its own position, so scaling the root would drag the measured centre offset with it.
        ShellArt.Scale = Vector3.One * ComputeFitScale(
            SelectExtent(Bounds, FitAxis),
            SelectArtExtent(_artSizeAtScale1, FitAxis),
            sizeUnits);
    }

    /// <summary>
    /// The body extent <paramref name="axis"/> names. Kept beside <see cref="SelectArtExtent"/>
    /// because both must be driven by the SAME axis value — reading a body's height while dividing
    /// by the art's width would scale by a ratio of unrelated quantities.
    /// </summary>
    /// <remarks>
    /// Under <see cref="ShellFitAxis.Largest"/> the two sides may resolve to different axes — the
    /// body's dominant extent against the art's dominant extent. That is the intent: it matches
    /// dominant-to-dominant so a shell covers a body however either is proportioned, without the
    /// shell needing to know the body's aspect.
    /// </remarks>
    public static float SelectExtent(VisualBounds3D bounds, ShellFitAxis axis) => axis switch
    {
        ShellFitAxis.Height => bounds.Height,
        ShellFitAxis.Width => bounds.Width,
        _ => bounds.Largest,
    };

    /// <summary>The shell art's own extent, selected by the same axis as <see cref="SelectExtent"/>.</summary>
    public static float SelectArtExtent(Vector2 artSize, ShellFitAxis axis) => axis switch
    {
        ShellFitAxis.Height => artSize.Y,
        ShellFitAxis.Width => artSize.X,
        _ => Mathf.Max(artSize.X, artSize.Y),
    };

    /// <summary>
    /// Node scale that renders <paramref name="overlayArtExtent"/> of art at
    /// <paramref name="sizeUnits"/> times the entity's extent along the same axis. Dividing the
    /// authored art extent out is what keeps the scene's pixel_size from changing the rendered size
    /// on a measured entity.
    /// </summary>
    public static float ComputeFitScale(float entityExtent, float overlayArtExtent, float sizeUnits)
    {
        // Zero size-units is legitimate authoring meaning "shrink to nothing", and is a DIFFERENT
        // case from an entity we could not measure — there the authored art must stand rather than
        // vanish. Collapsing the two would render a zero-unit size at FULL authored extent.
        if (sizeUnits <= 0f) { return 0f; }
        if (entityExtent <= 0f || overlayArtExtent <= 0f) { return 1f; }

        return entityExtent * sizeUnits / overlayArtExtent;
    }

    private void ApplyCenter()
    {
        if (ShellArt == null || !GodotObject.IsInstanceValid(ShellArt)) { return; }

        ShellArt.Position = new Vector3(ShellArt.Position.X, Bounds.CenterOffsetY, ShellArt.Position.Z);
    }
}
