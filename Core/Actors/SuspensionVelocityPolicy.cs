namespace Jmodot.Core.Actors;

/// <summary>
/// What a successful suspension claim does to the controller's velocity. Zeroing is OPT-IN: most
/// claimants hold the body briefly and want it to resume from the momentum it had, so only a
/// claimant that will drive the body somewhere else entirely — and whose release must not resume a
/// stale vector — asks for <see cref="Zero"/>.
/// </summary>
public enum SuspensionVelocityPolicy
{
    /// <summary>Velocity is untouched; the owner resumes with whatever the body already carried.</summary>
    Preserve,

    /// <summary>Velocity is zeroed at the moment the claim is taken.</summary>
    Zero,
}
