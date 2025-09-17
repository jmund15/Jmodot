namespace Jmodot.Core.Modifiers.Effects;

using Godot.Collections;

/// <summary>
///     A Resource defining a temporary effect (buff or debuff) that can be applied to an actor.
///     It contains the duration of the effect and a list of modifiers to apply while active.
/// </summary>
[GlobalClass]
public partial class EffectData : Resource
{
    /// <summary>
    ///     The duration of the effect in seconds. A value <= 0 means the effect is permanent until removed.</summary>
    [Export]
    public float Duration { get; private set; } = 5.0f;

    /// <summary>A list of data-driven modifier resources to apply to the target.</summary>
    [Export]
    public Array<Resource> Modifiers { get; private set; } = new();
}
