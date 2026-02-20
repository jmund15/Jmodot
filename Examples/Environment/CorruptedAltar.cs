namespace Jmodot.Examples.Environment;

using System;
using Core.Health;
using Jmodot.Implementation.Shared;

public partial class CorruptedAltar : Node, IHealable
{
    /// <summary>Starting corruption level. Also used as the max for health event args.</summary>
    [Export] private float _corruption = 100f;

    private float _maxCorruption;

    public event Action<HealthChangeEventArgs> OnHealed = delegate { };

    public override void _Ready()
    {
        base._Ready();
        _maxCorruption = _corruption;
    }

    public void Heal(float amount, object source)
    {
        if (amount <= 0 || _corruption <= 0) { return; }

        float previousCorruption = _corruption;
        _corruption = Mathf.Max(0, _corruption - amount);

        var args = new HealthChangeEventArgs(
            _maxCorruption - _corruption, _maxCorruption - previousCorruption, _maxCorruption, source);
        OnHealed.Invoke(args);

        if (_corruption <= 0)
        {
            JmoLogger.Info(this, "The Altar is cleansed!");
        }
    }
}
