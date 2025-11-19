namespace Jmodot.Examples.Environment;

using System;
using Core.Health;

public partial class CorruptedAltar : Node, IHealable
{
    [Export] private float _corruption = 100f;

    public event Action<HealthChangeEventArgs> OnHealed = delegate { };

    public void Heal(float amount, object source)
    {
        _corruption -= amount;

        var args = new HealthChangeEventArgs(100f - _corruption, 100f - (_corruption + amount), 100f, source);
        OnHealed?.Invoke(args);

        if (_corruption <= 0)
        {
            GD.Print("The Altar is cleansed!");
            // Emit a signal, change appearance, etc.
        }
    }
}
