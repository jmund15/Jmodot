namespace Jmodot.Core.Visual.Sprite;

using Godot;

/// <summary>
/// The mirror channel of a <see cref="FacingProfile3D"/>: how single-direction art is H-mirrored to
/// match a facing. Carries only what every mirror consumer reads, so no knob shown here is dead in any
/// of them.
/// </summary>
/// <remarks>
/// Configuration only — the mirror decision itself is <c>JmoMath.ShouldMirrorHorizontal</c>, and one
/// instance may be shared by reference across many consumers, so nothing here is per-consumer state.
/// </remarks>
[GlobalClass, Tool]
public partial class MirrorFacingConfig : Resource
{
    /// <summary>
    /// The direction the source art faces when unflipped. FlipH is applied whenever the current facing
    /// opposes this.
    /// </summary>
    [Export] public bool ArtFacesRight { get; private set; } = true;

    #region Test Helpers
#if TOOLS
    internal void SetForTest(bool artFacesRight)
    {
        this.ArtFacesRight = artFacesRight;
    }
#endif
    #endregion
}
