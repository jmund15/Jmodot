namespace Jmodot.Core.Visual.Sprite;

using Godot;

/// <summary>
/// An entity's facing answer, shared: which facing CHANNELS its art participates in. A driver reads only
/// its own channel, so a mirror driver never surfaces a rotation knob and vice versa.
/// </summary>
/// <remarks>
/// <para>
/// A configuration carrier only — the facing math stays in <c>JmoMath</c>, and this type computes
/// nothing. Both channels are optional and independent: mirror-only, rotation-only, both, or neither are
/// all authorable, and the last of those is the one <see cref="ValidateConfiguration"/> reports.
/// </para>
/// <para>
/// One profile may be referenced by many mounts, so it holds no per-consumer state; live mirror state
/// belongs to the consumer node. A NULL profile is meaningful and each consumer documents what it means
/// there — nothing may hand a consumer a synthesized default profile, which would erase that distinction.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class FacingProfile3D : Resource
{
    /// <summary>
    /// Mirror channel — read by drivers that H-mirror single-direction art. Leave empty for art that
    /// does not mirror.
    /// </summary>
    [Export] public MirrorFacingConfig? Mirror { get; private set; }

    /// <summary>
    /// Rotation channel — read by drivers that rotate art to face its travel direction. Leave empty for
    /// art that does not rotate.
    /// </summary>
    [Export] public RotationFacingConfig? Rotation { get; private set; }

    /// <summary>
    /// Returns the reason this profile answers nothing, or null when at least one channel is authored.
    /// Consumers surface the message in <c>_GetConfigurationWarnings</c> rather than throwing — an
    /// empty profile is an authoring mistake, not a broken scene.
    /// </summary>
    public string? ValidateConfiguration()
    {
        if (this.Mirror != null || this.Rotation != null) { return null; }

        return "This FacingProfile3D carries neither a Mirror nor a Rotation channel, so it configures "
            + "nothing for whoever reads it. Add the channel the consuming driver reads, or clear the "
            + "profile reference.";
    }

    #region Test Helpers
#if TOOLS
    internal void SetForTest(MirrorFacingConfig? mirror, RotationFacingConfig? rotation)
    {
        this.Mirror = mirror;
        this.Rotation = rotation;
    }
#endif
    #endregion
}
