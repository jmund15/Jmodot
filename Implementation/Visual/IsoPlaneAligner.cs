namespace Jmodot.Implementation.Visual;

using Godot;
using Jmodot.Core.Visual;

/// <summary>
/// Aligns this node's basis to the camera's screen plane, so sprite subtrees below it are
/// authored FLAT and inherit the iso tilt from one place — the live camera — rather than each
/// scene baking a literal tilt transform.
/// <para>
/// A baked tilt is a camera constant frozen into a scene asset: change the camera's pitch and
/// every scene carrying it is silently wrong, with nothing to catch the drift. Deriving from
/// <see cref="VisualProjectionDefaults.DepthForeshorten"/> (published by the active camera)
/// makes the frame follow the camera instead.
/// </para>
/// </summary>
/// <remarks>
/// Deliberately NOT <c>[Tool]</c>: this owns the node's basis at runtime, and running it in the
/// editor would fight the authored transform while the scene is being edited.
/// </remarks>
[GlobalClass]
public partial class IsoPlaneAligner : Node3D
{
    private float _appliedForeshorten = float.NaN;

    public override void _Ready()
    {
        base._Ready();
        ApplyIfChanged();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        // Polled rather than set once in _Ready: the publishing camera is not an ancestor, so
        // there is no lifecycle ordering that guarantees it has published by the time this runs.
        // The guard is a float compare — the basis is only rebuilt when the projection changes.
        ApplyIfChanged();
    }

    /// <summary>
    /// Screen-plane basis for a camera pitched <c>t</c> from horizontal, where
    /// <paramref name="depthForeshorten"/> is <c>sin t</c>. The camera sets its own rotation to
    /// <c>−t</c> about X (<c>FollowCam._Ready</c>), putting its local <c>+Z</c> — the direction it
    /// looks back along — at <c>(0, sin t, cos t)</c>. Sprite quads set <c>axis = 1</c>, so the
    /// quad normal is local <c>Y</c>; this basis turns a flat-authored plane by <c>90° − t</c> to
    /// land that normal on the view axis. Orthonormal at every pitch, including the top-down
    /// identity, where it correctly reduces to no rotation at all.
    /// <para>
    /// <paramref name="quadNormalLocal"/> is the quad's authored normal in this basis's local space
    /// — the caller's convention, named rather than assumed. It must lie in the local YZ plane
    /// (every quad convention in this project does). Omit it for the flat-authored <c>+Y</c> sprite
    /// quad described above. A <see cref="Label3D"/> carries no <c>axis</c> member — it is not a
    /// <c>SpriteBase3D</c> — and its quad faces local <c>+Z</c> (Godot's <c>MODEL_FRONT</c>; note
    /// <see cref="Vector3.Forward"/> is <c>−Z</c>, as it names a camera's facing, not a model's);
    /// handing it the default rolls its glyphs a quarter turn in the screen plane, which reads as
    /// wrong-facing text rather than as a frame error.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The complement is load-bearing and easy to get backwards: <c>asin</c> and <c>acos</c> agree
    /// at exactly one pitch, 45°, which is the project's only shipped camera angle. A wrong choice
    /// therefore passes every fixed-pitch test while tilting sprite planes at every other angle —
    /// the same silently-invisible failure class as a transposed basis. Pin the normal's vector
    /// across a pitch SWEEP, never a single angle.
    /// </remarks>
    public static Basis ComputeIsoBasis(float depthForeshorten, Vector3? quadNormalLocal = null)
    {
        float sinTilt = Mathf.Clamp(depthForeshorten, 0f, 1f);
        Vector3 quadNormal = quadNormalLocal?.Normalized() ?? Vector3.Up;
        // The normal's own lean from local Y, subtracted so the DECLARED column is the one that
        // lands on the view axis. The +Y convention leans 0, reducing this to the original acos form
        // exactly; +Z leans 90° and needs the extra quarter turn off.
        float lean = Mathf.Atan2(quadNormal.Z, quadNormal.Y);
        return new Basis(Vector3.Right, Mathf.Acos(sinTilt) - lean);
    }

    private void ApplyIfChanged()
    {
        // Until a camera publishes, the authored basis is the only trustworthy frame. The identity
        // default would otherwise be applied as a real (top-down) projection, rotating the subtree
        // 90° and rendering every sprite below it edge-on — invisible, silently.
        if (!VisualProjectionDefaults.IsPublished)
        {
            return;
        }

        float current = VisualProjectionDefaults.DepthForeshorten;
        if (Mathf.IsEqualApprox(current, _appliedForeshorten))
        {
            return;
        }

        _appliedForeshorten = current;
        // Orientation only. Assigning the bare basis would silently reset the node's authored
        // scale to 1 — this node sits above whole sprite subtrees, so that lands as a global
        // resize with no error.
        Basis = ComputeIsoBasis(current).Scaled(Scale);
    }
}
