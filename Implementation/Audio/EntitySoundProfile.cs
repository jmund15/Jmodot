namespace Jmodot.Implementation.Audio;

using Godot;
using Jmodot.Core.Audio;

/// <summary>
/// One entity's authored sound mapping: per-animation entries plus optional hit/death
/// overrides. Resolution follows the house Default Value Pattern
/// (<c>override ?? AudioDefaults.*</c>), so an unset override falls back to the universal
/// default. Immutable authored config — never mutated at play.
/// </summary>
[GlobalClass, Tool]
public partial class EntitySoundProfile : Resource
{
    /// <summary>Per-animation rows matched against AnimStarted events. Unknown animations are silently ignored.</summary>
    [Export] public EntitySoundEntry[] Entries { get; set; } = System.Array.Empty<EntitySoundEntry>();

    /// <summary>Per-entity hit override; null falls back to AudioDefaults.Hit.</summary>
    [Export] public SoundProfile? HitSound { get; set; }

    /// <summary>Per-entity death override; null falls back to AudioDefaults.Death.</summary>
    [Export] public SoundProfile? DeathSound { get; set; }

    /// <summary>Resolves the hit sound: the entity override, else the universal default.</summary>
    public SoundProfile? ResolveHitSound() => HitSound ?? AudioDefaults.Hit;

    /// <summary>Resolves the death sound: the entity override, else the universal default.</summary>
    public SoundProfile? ResolveDeathSound() => DeathSound ?? AudioDefaults.Death;
}
