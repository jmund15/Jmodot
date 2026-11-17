namespace Jmodot.Implementation.AI.HSM;

/// <summary>Whether a <see cref="State"/> faces its target while fallback animation owns the body.</summary>
public enum FaceTargetMode
{
    /// <summary>Defer to the nearest ancestor that sets Face or Don't Face; no facing if none does.</summary>
    Inherit = 0,

    /// <summary>Face the live target here and in the states below that inherit.</summary>
    Face = 1,

    /// <summary>Do not face the target here, even when an ancestor faces it.</summary>
    DontFace = 2,
}
