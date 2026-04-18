namespace Jmodot.Implementation.Movement.Strategies;

[GlobalClass]
public partial class MovementStrategyStatOverride2D : Resource
{
    [Export] public float MaxSpeed { get; set; }
    [Export] public float Acceleration { get; set; }
    [Export] public float Friction { get; set; }
}
