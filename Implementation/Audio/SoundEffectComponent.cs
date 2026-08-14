namespace Jmodot.Implementation.Audio;

using System;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Audio;
using Jmodot.Core.Components;
using Jmodot.Core.Health;
using Jmodot.Core.Visual.Animation.Sprite;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Shared;

/// <summary>
/// Plays the owning entity's audio in response to entity events. <c>AnimStarted</c> maps to a
/// profile entry (<see cref="PlayMode.OneShot"/> once, <see cref="PlayMode.Cadence"/> re-issued
/// while the animation runs); health hit events play the resolved hit sound behind a
/// <see cref="DamageKind"/> filter; death plays the resolved death sound. All audio is feedback:
/// a missing profile, seam, or entry sound degrades to silence with a one-time warning, never a
/// throw.
/// </summary>
/// <remarks>
/// Deps are both soft. An absent <see cref="BBDataSig.AnimationOrchestrator"/> leaves the anim side
/// inert; an absent <see cref="BBDataSig.HealthComponent"/> leaves no health subscription.
/// <see cref="Initialize"/> returns false only when NEITHER resolves — a component with nothing
/// to drive is the one true misconfiguration. The director is read lazily from
/// <see cref="AudioSeam"/> at play-time, never cached at init, which is what lets a test swap a spy
/// director through the seam after load.
/// Subscriptions land in <see cref="OnPostInitialize"/> and are idempotency-safe (unsubscribe
/// then subscribe) because the underlying events are C# events.
/// </remarks>
[GlobalClass, Tool]
public partial class SoundEffectComponent : Node, IComponent
{
    /// <summary>Per-entity sound mapping. Null no-ops events with a one-time warning.</summary>
    [Export] public EntitySoundProfile? Profile { get; set; }

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    private IAnimationOrchestrator? _animationOrchestrator;
    private IHealth? _health;
    private Timer? _cadenceTimer;
    private StringName _cadenceAnim = new();
    private bool _warnedNullProfile;
    private bool _warnedNullSeam;
    private bool _warnedNullEntrySound;

    public bool Initialize(IBlackboard bb)
    {
        if (bb.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out var anim) && anim != null)
        {
            _animationOrchestrator = anim;
        }
        if (bb.TryGet<IHealth>(BBDataSig.HealthComponent, out var health) && health != null)
        {
            _health = health;
        }
        if (_animationOrchestrator == null && _health == null)
        {
            // ENCI owns the single Error on a false return; the component supplies the which-key detail at Debug.
            JmoLogger.Debug(this, "[Audio] Neither BBDataSig.AnimationOrchestrator nor BBDataSig.HealthComponent resolved; component has nothing to drive.");
            return false;
        }
        IsInitialized = true;
        Initialized();
        return true;
    }

    public void OnPostInitialize() => Subscribe();

    public override void _ExitTree()
    {
        base._ExitTree();
        UnsubscribeOrchestrator();
        UnsubscribeHealth();
        if (_cadenceTimer != null)
        {
            _cadenceTimer.Stop();
            _cadenceTimer.QueueFree();
            _cadenceTimer = null;
        }
    }

    public Node GetUnderlyingNode() => this;

    private void Subscribe()
    {
        if (_animationOrchestrator != null)
        {
            _animationOrchestrator.AnimStarted -= OnAnimStarted;
            _animationOrchestrator.AnimStarted += OnAnimStarted;
            _animationOrchestrator.AnimStopped -= OnAnimStopped;
            _animationOrchestrator.AnimStopped += OnAnimStopped;
        }
        if (_health != null)
        {
            _health.OnHealthChanged -= OnHealthChanged;
            _health.OnHealthChanged += OnHealthChanged;
            _health.OnDied -= OnDied;
            _health.OnDied += OnDied;
        }
    }

    private void UnsubscribeOrchestrator()
    {
        var anim = _animationOrchestrator;
        if (anim == null || (anim is GodotObject go && !GodotObject.IsInstanceValid(go)))
        {
            return;
        }
        anim.AnimStarted -= OnAnimStarted;
        anim.AnimStopped -= OnAnimStopped;
    }

    private void UnsubscribeHealth()
    {
        var health = _health;
        if (health == null || (health is GodotObject healthGo && !GodotObject.IsInstanceValid(healthGo)))
        {
            return;
        }
        health.OnHealthChanged -= OnHealthChanged;
        health.OnDied -= OnDied;
    }

    private void OnAnimStarted(StringName animName)
    {
        // One active cadence per component — any new anim supersedes the previous one.
        CancelCadence();
        var profile = Profile;
        if (profile == null)
        {
            WarnOnce(ref _warnedNullProfile, "EntitySoundProfile is null; sound events ignored.");
            return;
        }
        var entry = FindEntry(profile, animName);
        if (entry == null)
        {
            return; // Unknown animation silently ignored (documented design rule).
        }
        if (entry.Sound == null)
        {
            WarnOnce(ref _warnedNullEntrySound, $"EntitySoundEntry '{animName}' has a null Sound; request dropped.");
            return;
        }
        var director = ResolveDirector();
        if (director == null)
        {
            return;
        }
        PlaySound(director, entry.Sound);
        if (entry.PlayMode == PlayMode.Cadence)
        {
            StartCadence(entry.Sound, entry.CadenceInterval, animName);
        }
    }

    private void OnAnimStopped(StringName animName) => CancelCadence();

    private void OnHealthChanged(HealthChangeEventArgs args)
    {
        // Only Direct/Reaction hits play the hit sound; DOT ticks and environmental damage have
        // their own per-tick feedback and must not stack the hit SFX. Heal is not damage.
        if (args.Kind != DamageKind.Direct && args.Kind != DamageKind.Reaction)
        {
            return;
        }
        var profile = Profile;
        if (profile == null)
        {
            WarnOnce(ref _warnedNullProfile, "EntitySoundProfile is null; sound events ignored.");
            return;
        }
        var sound = profile.ResolveHitSound();
        if (sound == null)
        {
            return;
        }
        var director = ResolveDirector();
        if (director == null)
        {
            return;
        }
        PlaySound(director, sound);
    }

    private void OnDied(HealthChangeEventArgs args)
    {
        var profile = Profile;
        if (profile == null)
        {
            WarnOnce(ref _warnedNullProfile, "EntitySoundProfile is null; sound events ignored.");
            return;
        }
        var sound = profile.ResolveDeathSound();
        if (sound == null)
        {
            return;
        }
        var director = ResolveDirector();
        if (director == null)
        {
            return;
        }
        PlaySound(director, sound);
    }

    /// <summary>
    /// Lazily reads the director from the seam and guards the freed-provider state: a forwarded
    /// director is never freed without resetting the seam, and when one is, the stale ref must
    /// drop here (with the warning) instead of throwing through the call.
    /// </summary>
    private IAudioDirector? ResolveDirector()
    {
        var director = AudioSeam.Director;
        if (director == null)
        {
            WarnOnce(ref _warnedNullSeam, "AudioSeam.Director is null; sound request dropped.");
            return null;
        }
        if (director is GodotObject go && !GodotObject.IsInstanceValid(go))
        {
            WarnOnce(ref _warnedNullSeam, "AudioSeam.Director is no longer valid; sound request dropped.");
            return null;
        }
        return director;
    }

    private void PlaySound(IAudioDirector director, SoundProfile sound)
        => director.Play(new SoundRequest(sound, CurrentPosition()));

    private Vector3 CurrentPosition()
        => GetParent<Node3D>()?.GlobalPosition ?? Vector3.Zero;

    private void StartCadence(SoundProfile sound, float interval, StringName animName)
    {
        EnsureCadenceTimer();
        _cadenceAnim = animName;
        _cadenceTimer!.WaitTime = System.Math.Max(0.01f, interval);
        _cadenceTimer.Start();
    }

    private void CancelCadence()
    {
        if (_cadenceTimer != null)
        {
            _cadenceTimer.Stop();
        }
        _cadenceAnim = new();
    }

    private void EnsureCadenceTimer()
    {
        if (_cadenceTimer != null)
        {
            return;
        }
        _cadenceTimer = new Timer { OneShot = false };
        AddChild(_cadenceTimer);
        _cadenceTimer.Timeout += OnCadenceTick;
    }

    private void OnCadenceTick()
    {
        var director = ResolveDirector();
        var profile = Profile;
        if (director == null || profile == null)
        {
            CancelCadence();
            return;
        }
        var entry = FindEntry(profile, _cadenceAnim);
        if (entry?.Sound == null)
        {
            CancelCadence();
            return;
        }
        PlaySound(director, entry.Sound);
    }

    private static EntitySoundEntry? FindEntry(EntitySoundProfile profile, StringName animName)
    {
        foreach (var entry in profile.Entries)
        {
            if (entry != null && entry.Animation == animName)
            {
                return entry;
            }
        }
        return null;
    }

    private void WarnOnce(ref bool fired, string message)
    {
        if (fired)
        {
            return;
        }
        fired = true;
        JmoLogger.Warning(this, message);
    }
}
