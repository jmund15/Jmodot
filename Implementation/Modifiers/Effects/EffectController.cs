namespace Jmodot.Implementation.Modifiers.Effects;

using System.Collections.Generic;
using Core.Modifiers.Effects;

/// <summary>
///     A component that manages the application and removal of timed effects (buffs/debuffs) on an actor.
///     It is responsible for handling durations and applying/removing modifiers from other systems.
/// </summary>
public partial class EffectController : Node
{
    private readonly List<ActiveEffect> _activeEffects = new();

    // This would be the target for the modifiers, e.g., the player's stat controller.
    // This dependency should be injected in a real implementation.
    private IStatController _statController; // Example interface

    public void ApplyEffect(EffectData effectData)
    {
        // Apply all modifiers immediately.
        // A more complex system would have different controllers for different modifier types.
        // foreach(var modResource in effectData.Modifiers)
        // {
        //     if (modResource is IModifier<float> floatMod)
        //     {
        //         // Figure out which property to apply it to...
        //         // TODO: design a system that intellegently routes effects to the right properties
        //     }
        // }

        // If the effect has a duration, set up a timer to remove it.
        if (effectData.Duration > 0)
        {
            var timer = new Timer { WaitTime = effectData.Duration, Autostart = true, OneShot = true };
            var activeEffect = new ActiveEffect { Data = effectData, Timer = timer };
            timer.Timeout += () => this.OnEffectExpired(activeEffect);
            this.AddChild(timer);
            this._activeEffects.Add(activeEffect);
        }
    }

    private void OnEffectExpired(ActiveEffect effect)
    {
        // Remove the modifiers that this effect applied.
        // foreach(var modResource in effect.Data.Modifiers) ...

        this._activeEffects.Remove(effect);
        effect.Timer.QueueFree();
    }

    // A struct to track the state of a currently active effect.
    private class ActiveEffect
    {
        public EffectData Data;
        public Timer Timer;
    }
}

// A placeholder for a stat controller interface to show the dependency.
