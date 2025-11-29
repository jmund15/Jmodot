using Godot;
using Jmodot.Core.Combat;
using Jmodot.Implementation.Combat;
using Jmodot.Implementation.Health;
using Jmodot.Implementation.AI.BB; // For BBDataSig

namespace Jmodot.Implementation.Combat.Status;

public class DoTStatus : TimedStatus
{
    public float DamagePerTick { get; private set; }
    public float TickInterval { get; private set; }

    private float _tickTimer;

    public DoTStatus(float duration, float tickInterval, float damagePerTick, Node source, ICombatant target)
        : base(duration, source, target)
    {
        TickInterval = tickInterval;
        DamagePerTick = damagePerTick;
        _tickTimer = 0f;
    }

    public override void OnTick(double delta)
    {
        base.OnTick(delta); // Handles TimeRemaining

        _tickTimer += (float)delta;
        if (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            ApplyDamage();
        }
    }

    private void ApplyDamage()
    {
        // Use Blackboard to get HealthComponent
        if (Target.Blackboard.TryGet(BBDataSig.HealthComp, out HealthComponent health))
        {
            health.TakeDamage(DamagePerTick, Source);
        }
    }
}
