namespace Jmodot.Implementation.Environment;

using Godot;
using Jmodot.Core.Environment;
using Jmodot.Core.Environment.FlowField;

/// <summary>
///     The physics presence of one flow-field stream: a coarse monitorable volume whose force is sampled
///     per-target from the bound entity's segment chain rather than from the shape itself.
/// </summary>
/// <remarks>
///     Named for the field rather than the seam because the profile's <see cref="FlowForceFlavor" /> chooses
///     which of the two interfaces returns a non-zero vector at runtime — a <c>*ForceArea</c> name would
///     assert a flavour a VelocityOffset-configured instance does not have. The mirrored sibling pairs avoid
///     this by splitting into two classes; one dual-seam class is deliberate here, so the name stays neutral.
/// </remarks>
[GlobalClass]
public partial class FlowFieldArea : Area3D, IForceProvider3D, IVelocityOffsetProvider3D
{
    private FlowFieldEntity? _entity;
    private IFlowFieldProfile? _profile;
    private FlowForceFlavor _flavor;

    /// <inheritdoc />
    public bool IsCaptureForce { get; private set; }

    /// <inheritdoc />
    public bool IsCaptureOffset { get; private set; }

    /// <summary>
    ///     Adopts an entity: tables, flavour and capture all arrive through <see cref="FlowFieldEntity.Profile" />.
    /// </summary>
    /// <remarks>
    ///     BOTH capture flags are written from the single profile flag. They are separate interface members with
    ///     independent <c>=&gt; false</c> defaults and the receiver runs a separate capture enumerator per flag,
    ///     so setting only the one matching the current flavour would compile, push correctly, and silently
    ///     never trigger control loss the moment the profile's flavour flipped.
    /// </remarks>
    public void Bind(FlowFieldEntity entity)
    {
        this._entity = entity;
        this._profile = entity.Profile;
        this._flavor = entity.Profile?.Flavor ?? FlowForceFlavor.Force;

        var isCapture = entity.Profile?.IsCapture ?? false;
        this.IsCaptureForce = isCapture;
        this.IsCaptureOffset = isCapture;
    }

    /// <summary>
    ///     Full unbind. Runs on every return path — clear-all and natural dormancy expiry alike — so a recycled
    ///     area can never sample a retired entity's chain.
    /// </summary>
    public void Release()
    {
        this._entity = null;
        this._profile = null;
        this._flavor = FlowForceFlavor.Force;
        this.IsCaptureForce = false;
        this.IsCaptureOffset = false;
    }

    /// <inheritdoc />
    public Vector3 GetForceFor(Node3D target)
    {
        return this._flavor == FlowForceFlavor.Force ? this.SampleAt(target) : Vector3.Zero;
    }

    /// <inheritdoc />
    public Vector3 GetVelocityOffsetFor(Node3D target)
    {
        return this._flavor == FlowForceFlavor.VelocityOffset ? this.SampleAt(target) : Vector3.Zero;
    }

    private Vector3 SampleAt(Node3D? target)
    {
        if (target == null || this._entity == null || this._profile == null)
        {
            return Vector3.Zero;
        }

        return FlowFieldMath.SampleVelocity(this._entity.Segments, target.GlobalPosition,
            this._profile.MaxEnergyPerSegment, this._profile.RadialFalloff01, this._profile.EnergyToSpeed01);
    }
}
