namespace Jmodot.Implementation.AI.Squad;

using Godot;
using Core.AI.Squad;
using Core.Shared.Attributes;

/// <summary>
/// Reference <see cref="SquadPolicy"/>: prescribes <c>_directiveWhenBelow</c> when the squad's mean
/// health fraction is strictly below <c>_belowFraction</c>; abstains at or above the threshold.
/// Both the threshold and the prescribed directive are data — no hardcoded tag semantics.
/// </summary>
[GlobalClass, Tool]
public partial class HealthThresholdPolicy : SquadPolicy
{
    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    private float _belowFraction = 0.25f;

    [Export, RequiredExport]
    private SquadDirectiveDefinition _directiveWhenBelow = null!;

    public override SquadDirectiveDefinition? Evaluate(in SquadSnapshot snapshot)
    {
        // Resources have no _Ready — fail fast at entry on a mis-authored / cloned .tres.
        this.ValidateRequiredExports();
        return snapshot.AverageHealthFraction < _belowFraction ? _directiveWhenBelow : null;
    }
}
