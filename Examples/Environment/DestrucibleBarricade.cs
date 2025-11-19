namespace Jmodot.Examples.Environment;

using System;
using Core.Health;

public partial class DestructibleBarricade : Node, IDamageable
{
    [Export] private float _durability = 100f;

    public event Action<HealthChangeEventArgs> OnDamaged = delegate { };

    public void TakeDamage(float amount, object source)
    {
        _durability -= amount;

        // We can still use the event args for consistency
        var args = new HealthChangeEventArgs(_durability, _durability + amount, 100f, source);
        OnDamaged?.Invoke(args);

        if (_durability <= 0)
        {
            QueueFree(); // Destroy the barricade
        }
    }
}
