namespace Jmodot.Implementation.Combat;

using AI.BB;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Combat;
using Jmodot.Core.Combat;
using Shared;

/// <summary>
/// Listens to the Combatant and pushes results into the Blackboard's Event Log.
/// </summary>
[GlobalClass]
public partial class CombatLogger : Node, IComponent
{
    [Export] public CombatantComponent Combatant { get; private set; }

    // The key where we store the Log object in the BB
    public const string BB_CombatLog = "CombatLogger";

    private CombatLog _log;

    public bool Initialize(IBlackboard bb)
    {
        // 1. Create or Retrieve the Log
        if (!bb.TryGet(BB_CombatLog, out _log))
        {
            _log = new CombatLog();
            bb.Set(BB_CombatLog, _log);
        }

        // 2. Resolve Dependency
        if (!bb.TryGet<CombatantComponent>(BBDataSig.CombatantComponent, out var c))
        {
            JmoLogger.Error(this, $"CombatLogger must have a Combatant Component to operate!");
            return false;
        }
        Combatant = c!;

        IsInitialized = true;
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        Combatant.CombatResultEvent += HandleCombatResultEvent;
    }

    private void HandleCombatResultEvent(CombatResult result)
    {
        _log?.Log(result);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Optional: Keep memory usage low
        //_log?.PruneAllButCurrentFrame();
    }

    public override void _ExitTree()
    {
        if (Combatant != null)
        {
            Combatant.CombatResultEvent -= HandleCombatResultEvent;
        }
    }

    // ... IComponent ...
    public bool IsInitialized { get; private set; }
    public event System.Action Initialized;
    public Node GetUnderlyingNode() => this;
}
