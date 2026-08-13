namespace Jmodot.Core.Visual.Sprite;

using Godot;

/// <summary>
/// The rotation channel of a <see cref="FacingProfile3D"/>: how art that ROTATES to face travel is
/// oriented. Orthogonal to the mirror channel — an entity may participate in either, both, or neither.
/// </summary>
/// <remarks>
/// Configuration only — the facing angle itself is <c>JmoMath.IsoPlaneFacingAngle</c>, and one instance
/// may be shared by reference across many consumers, so nothing here is per-consumer state.
/// </remarks>
[GlobalClass, Tool]
public partial class RotationFacingConfig : Resource
{
    /// <summary>
    /// Degrees the source art's nose sits away from screen-right when unrotated. Added to the computed
    /// facing angle, so art drawn nose-up authors 90 here.
    /// </summary>
    [Export] public float ArtBaseAngleOffsetDegrees { get; private set; } = 0f;

    #region Test Helpers
#if TOOLS
    internal void SetForTest(float artBaseAngleOffsetDegrees)
    {
        this.ArtBaseAngleOffsetDegrees = artBaseAngleOffsetDegrees;
    }
#endif
    #endregion
}
