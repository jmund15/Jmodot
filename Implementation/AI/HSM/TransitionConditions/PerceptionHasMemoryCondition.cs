namespace Jmodot.Implementation.AI.HSM.TransitionConditions;

using System.Linq;
using Core.AI.BB;
using Core.AI.HSM;
using Core.Identification;
using Core.Shared.Attributes;
using Implementation.AI.BB;
using Perception;
using Shared;

/// <summary>
/// TransitionCondition that checks whether the AIPerceptionManager3D has active
/// memories for a given Category. Replaces BB bool flags (e.g., Critter_Threatened)
/// for perception-driven critters.
///
/// When Value=true: returns true if any active memory exists for the category (→ "threatened")
/// When Value=false: returns true if NO active memories exist (→ "safe, return to wander")
/// </summary>
[GlobalClass]
public partial class PerceptionHasMemoryCondition : TransitionCondition
{
    [Export, RequiredExport] private Category _category = null!;
    [Export] public bool Value { get; private set; } = true;

    private bool _diagLogged;

    public override bool Check(Node agent, IBlackboard bb)
    {
        if (!bb.TryGet<AIPerceptionManager3D>(BBDataSig.PerceptionComp, out var perception))
        {
            if (!_diagLogged)
            {
                JmoLogger.Warning(this, $"PerceptionHasMemoryCondition: No PerceptionComp on BB (category={_category?.CategoryName})");
                _diagLogged = true;
            }
            return false;
        }

        bool hasMemory = perception.GetSensedByCategory(_category).Any();

        // Diagnostic: one-shot log on first mismatch to identify root cause
        if (!_diagLogged && hasMemory != Value)
        {
            var allMemories = perception.GetAllActiveMemories().ToList();
            JmoLogger.Info(this,
                $"[DIAG] PerceptionHasMemory: category={_category?.CategoryName} " +
                $"catHash={_category?.GetHashCode()} value={Value} hasMemory={hasMemory} " +
                $"activeMemoryCount={allMemories.Count}");
            foreach (var mem in allMemories)
            {
                var cats = mem.Identity?.Categories;
                if (cats != null)
                {
                    foreach (var c in cats)
                    {
                        JmoLogger.Info(this,
                            $"[DIAG]   memory={mem.Identity?.IdentityName} cat={c?.CategoryName} " +
                            $"catHash={c?.GetHashCode()} sameRef={ReferenceEquals(c, _category)}");
                    }
                }
            }
            _diagLogged = true;
        }

        return hasMemory == Value;
    }

    #region Test Helpers
#if TOOLS
    internal void SetCategory(Category category) => _category = category;
    internal void SetValue(bool value) => Value = value;
#endif
    #endregion
}
