namespace Jmodot.Implementation.AI.BehaviorTree.Composites;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI;
using Core.AI.BB;
using Shared;
using Shared.GodotExceptions;
using Tasks;

public enum EngagePhase { Reposition, Position, Commit, Attack, Recover, Miss }

/// <summary>
/// How a phase's child ended. <see cref="Refused"/> is a Failure that arrived synchronously inside the
/// child's own <c>Enter()</c> (a refused guard or an <c>OnEnter</c> that fails); <see cref="Failed"/> is a
/// Failure after the child had been Running.
/// </summary>
public enum PhaseOutcome { Succeeded, Failed, Refused }

public readonly record struct EngageSlots(
    bool Reposition, bool Position, bool Commit, bool Attack, bool Recover, bool Miss);

/// <summary>
/// One fight cycle: reposition → position → commit → attack → recover (or miss) → back to the entry phase.
/// Each phase is a slot holding a direct child; unset slots are skipped. The next phase depends on how the
/// last one ended, which a <see cref="Sequence"/> or <see cref="Selector"/> cannot express: a refused attack
/// restarts without recovery, a missed attack enters Miss, a failed Position hands the parent its fallback.
/// <para>
/// The cycle never succeeds. It fails only when Position fails (or a refusal chain exceeds
/// <see cref="MaxTransitionsPerTick"/>), so place it before a pursuing sibling in a
/// <see cref="Selector"/>/<see cref="ReactiveSelector"/>. Its own <see cref="BehaviorTask.Conditions"/> gate
/// entry only: <see cref="BehaviorTask.MonitorConditions"/> is off, so a mid-phase condition never aborts a
/// swing. Phases own their monitored guards.
/// </para>
/// </summary>
[GlobalClass, Tool]
public partial class EngageCycle : CompositeTask
{
    /// <summary>More transitions than this inside one tick fail the cycle: a refusal chain would otherwise spin.</summary>
    public const int MaxTransitionsPerTick = 6;

    /// <summary>First phase of every entry: where the creature moves before the next attempt. Optional.</summary>
    [Export] public BehaviorTask? Reposition { get; set; }

    /// <summary>Holds the creature near its target before it commits. Its failure ends the cycle. Optional.</summary>
    [Export] public BehaviorTask? Position { get; set; }

    /// <summary>Closes from the position to attack range. Its failure restarts at the entry phase. Optional.</summary>
    [Export] public BehaviorTask? Commit { get; set; }

    /// <summary>The attack itself. Required.</summary>
    [Export] public BehaviorTask? Attack { get; set; }

    /// <summary>Runs after an attack that succeeded. Optional.</summary>
    [Export] public BehaviorTask? Recover { get; set; }

    /// <summary>Runs after an attack that failed after starting (a miss). Optional.</summary>
    [Export] public BehaviorTask? Miss { get; set; }

    /// <summary>Raised on every transition: from, to (null when the cycle fails), outcome, seconds spent in <c>from</c>.</summary>
    public event Action<EngagePhase, EngagePhase?, PhaseOutcome, float> PhaseChanged = delegate { };

    private EngagePhase _phase;
    private BehaviorTask? _running;
    private float _secondsInPhase;
    private int _transitionsThisTick;
    private bool _entering;
    private TaskStatus? _statusDuringEnter;

    public EngageCycle()
    {
        MonitorConditions = false;
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() == nameof(MonitorConditions))
        {
            property["usage"] = (long)PropertyUsageFlags.NoEditor;
        }
    }

    /// <summary>The phase every pass starts in: the first present of Reposition, Position, Commit, Attack.</summary>
    /// <exception cref="ArgumentException"><paramref name="present"/> has no Attack slot.</exception>
    public static EngagePhase Entry(EngageSlots present)
    {
        RequireAttack(present);
        if (present.Reposition) { return EngagePhase.Reposition; }
        if (present.Position) { return EngagePhase.Position; }
        return present.Commit ? EngagePhase.Commit : EngagePhase.Attack;
    }

    /// <summary>
    /// Pure transition rule. Null means the cycle fails. <paramref name="outcome"/> is ignored when leaving
    /// Reposition, Recover and Miss; Position, Commit and Attack branch on it.
    /// </summary>
    public static EngagePhase? Next(EngagePhase from, PhaseOutcome outcome, EngageSlots present)
    {
        var entry = Entry(present);
        switch (from)
        {
            case EngagePhase.Reposition:
                if (present.Position) { return EngagePhase.Position; }
                return present.Commit ? EngagePhase.Commit : EngagePhase.Attack;
            case EngagePhase.Position:
                if (outcome != PhaseOutcome.Succeeded) { return null; }
                return present.Commit ? EngagePhase.Commit : EngagePhase.Attack;
            case EngagePhase.Commit:
                return outcome == PhaseOutcome.Succeeded ? EngagePhase.Attack : entry;
            case EngagePhase.Attack:
                return outcome switch
                {
                    PhaseOutcome.Succeeded => present.Recover ? EngagePhase.Recover : entry,
                    PhaseOutcome.Failed => present.Miss ? EngagePhase.Miss : present.Recover ? EngagePhase.Recover : entry,
                    _ => entry,
                };
            default:
                return entry;
        }
    }

    private static void RequireAttack(EngageSlots present)
    {
        if (!present.Attack)
        {
            throw new ArgumentException("An engage slot set must include Attack.", nameof(present));
        }
    }

    private EngageSlots Slots => new(
        Reposition != null, Position != null, Commit != null, Attack != null, Recover != null, Miss != null);

    private BehaviorTask SlotFor(EngagePhase phase) => phase switch
    {
        EngagePhase.Reposition => Reposition!,
        EngagePhase.Position => Position!,
        EngagePhase.Commit => Commit!,
        EngagePhase.Attack => Attack!,
        EngagePhase.Recover => Recover!,
        _ => Miss!,
    };

    private IEnumerable<(string Slot, BehaviorTask? Task)> SlotTasks()
    {
        yield return (nameof(Reposition), Reposition);
        yield return (nameof(Position), Position);
        yield return (nameof(Commit), Commit);
        yield return (nameof(Attack), Attack);
        yield return (nameof(Recover), Recover);
        yield return (nameof(Miss), Miss);
    }

    public override void Init(Node agent, IBlackboard bb)
    {
        base.Init(agent, bb);
        var errors = ConfigurationErrors().ToList();
        if (errors.Count > 0)
        {
            throw new NodeConfigurationException(string.Join(" ", errors), this);
        }
    }

    private IEnumerable<string> ConfigurationErrors()
    {
        if (Attack == null) { yield return "EngageCycle requires its Attack slot."; }

        var seen = new HashSet<BehaviorTask>();
        foreach (var (slot, task) in SlotTasks())
        {
            if (task == null) { continue; }
            if (task.GetParent() != this) { yield return $"Slot {slot} ('{task.Name}') is not a direct child of the cycle."; }
            if (!seen.Add(task)) { yield return $"Slot {slot} reuses '{task.Name}', which another slot holds."; }
        }
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = ConfigurationErrors().ToList();
        var slotted = SlotTasks().Select(s => s.Task).OfType<BehaviorTask>().ToHashSet();
        foreach (var child in GetChildren().OfType<BehaviorTask>().Where(c => !slotted.Contains(c)))
        {
            warnings.Add($"Child '{child.Name}' is in no slot and will never run.");
        }

        var siblings = GetParent()?.GetChildren().OfType<BehaviorTask>().ToList();
        var hasFallback = GetParent() is Selector or ReactiveSelector
                          && siblings != null && siblings.IndexOf(this) < siblings.Count - 1;
        if (!hasFallback)
        {
            warnings.Add("EngageCycle fails when Position fails; place it in a Selector/ReactiveSelector with a later sibling.");
        }

        return warnings.Concat(base._GetConfigurationWarnings()).ToArray();
    }

    protected override void OnEnter()
    {
        base.OnEnter();
        _transitionsThisTick = 0;
        StartPhase(Entry(Slots));
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_running != null)
        {
            _running.TaskStatusChanged -= OnChildStatusChanged;
            _running.Exit();
            _running = null;
        }
    }

    protected override void OnProcessPhysics(float delta)
    {
        _transitionsThisTick = 0;
        _secondsInPhase += delta;
        _running?.ProcessPhysics(delta);
    }

    protected override void OnProcessFrame(float delta)
    {
        _transitionsThisTick = 0;
        _running?.ProcessFrame(delta);
    }

    private void StartPhase(EngagePhase phase)
    {
        while (true)
        {
            _phase = phase;
            _secondsInPhase = 0f;
            var child = SlotFor(phase);
            _running = child;
            child.TaskStatusChanged += OnChildStatusChanged;
            _statusDuringEnter = null;
            _entering = true;
            child.Enter();
            _entering = false;

            if (_statusDuringEnter is not { } ended) { return; }

            child.TaskStatusChanged -= OnChildStatusChanged;
            child.Exit();
            _running = null;
            var outcome = ended == TaskStatus.Success ? PhaseOutcome.Succeeded : PhaseOutcome.Refused;
            if (Advance(outcome) is not { } next) { return; }
            phase = next;
        }
    }

    private void OnChildStatusChanged(TaskStatus newStatus)
    {
        if (newStatus is TaskStatus.Running or TaskStatus.Fresh) { return; }
        if (_entering)
        {
            _statusDuringEnter = newStatus;
            return;
        }

        var child = _running;
        if (child == null) { return; }
        child.TaskStatusChanged -= OnChildStatusChanged;
        child.Exit();
        _running = null;

        var outcome = newStatus == TaskStatus.Success ? PhaseOutcome.Succeeded : PhaseOutcome.Failed;
        if (Advance(outcome) is { } next) { StartPhase(next); }
    }

    /// <summary>Logs and routes one finished phase; returns the next phase, or null after failing the cycle.</summary>
    private EngagePhase? Advance(PhaseOutcome outcome)
    {
        var from = _phase;
        var next = Next(from, outcome, Slots);
        if (++_transitionsThisTick > MaxTransitionsPerTick)
        {
            JmoLogger.Warning(this, $"[Engage] {Agent.Name} more than {MaxTransitionsPerTick} transitions in one tick; failing the cycle.");
            next = null;
        }

        var to = next is { } n ? n.ToString() : "exit";
        JmoLogger.Info(this, $"[Engage] {Agent.Name} {from} → {to} ({outcome}, {_secondsInPhase:F2} s)");
        PhaseChanged.Invoke(from, next, outcome, _secondsInPhase);

        if (next == null) { Status = TaskStatus.Failure; }
        return next;
    }
}
