namespace Jmodot.Examples.Environment;

using System;
using Core.Health;

public partial class DestructibleBarricade : Node, IDamageable
{
    /// <summary>Starting durability. Also used as the max for health event args.</summary>
    [Export] private float _durability = 100f;

    private float _maxDurability;

    public event Action<HealthChangeEventArgs> OnDamaged = delegate { };

    public override void _EnterTree()
    {
        base._EnterTree();
        // _EnterTree fires before any sibling's _Ready, so a TakeDamage call from
        // a deferred-spawn neighbor sees a populated _maxDurability.
        if (_maxDurability == 0f) { _maxDurability = _durability; }
    }

    public void TakeDamage(float amount, object source, DamageKind kind = DamageKind.Direct)
    {
        if (amount <= 0 || _durability <= 0) { return; }

        float previousDurability = _durability;
        _durability = Mathf.Max(0, _durability - amount);

        var args = new HealthChangeEventArgs(_durability, previousDurability, _maxDurability, source, kind);
        OnDamaged.Invoke(args);

        if (_durability <= 0)
        {
            QueueFree();
        }
    }
}
