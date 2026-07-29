namespace Jmodot.Implementation.Movement.Approach;

using Godot;

/// <summary>
/// Abstract base Resource for "glide from here to there" motion rules. An approach profile answers
/// two questions each physics frame: where should the mover be after this step, and has it arrived?
///
/// <para>
/// <b>Stateless:</b> one <c>.tres</c> instance is shared by every entity that authors it, so a profile
/// holds ZERO per-consumer state. Elapsed time is a parameter, never a field — two movers stepping the
/// same instance in the same frame must not perturb each other.
/// </para>
///
/// <para>
/// <b>Subclass rules:</b> concrete subclasses MUST be marked <c>[GlobalClass, Tool]</c> — otherwise
/// <c>.tres</c> files deserialize as bare <see cref="Resource"/> and throw
/// <see cref="System.InvalidCastException"/> on type-checked access.
/// </para>
/// </summary>
[GlobalClass, Tool]
public abstract partial class ApproachProfile3D : Resource
{
    /// <summary>Position after one step from <paramref name="current"/> toward <paramref name="target"/>.</summary>
    /// <param name="current">The mover's position at the start of this step.</param>
    /// <param name="target">The live target position; it may move between steps.</param>
    /// <param name="elapsed">Seconds since the approach began, owned and accumulated by the caller.</param>
    /// <param name="delta">Frame delta in seconds.</param>
    public abstract Vector3 Step(Vector3 current, Vector3 target, float elapsed, float delta);

    /// <summary>True once the approach should be treated as arrived.</summary>
    public abstract bool IsComplete(Vector3 current, Vector3 target, float elapsed);
}
