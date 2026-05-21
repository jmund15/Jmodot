namespace Jmodot.Implementation.Interaction;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.Components;
using Core.Input;
using Core.Interaction;
using Core.Shared.Attributes;
using Implementation.AI.BB;
using Implementation.Shared;

/// <summary>
/// Detects <see cref="IInteractable3D"/> objects in range and dispatches the configured
/// <see cref="InteractAction"/> to the nearest eligible one. Replaces the interaction-dispatch
/// responsibility formerly bolted onto the grab component, as a reusable framework primitive.
///
/// <para>
/// Maintains an in-range set via Area signals, recomputes the nearest target that passes
/// <see cref="IInteractable3D.CanInteract"/> each frame, and drives the optional
/// <see cref="IInteractionFeedbackProvider3D"/> feedback lifecycle on target changes.
/// On a just-pressed <see cref="InteractAction"/> it invokes <see cref="IInteractable3D.Interact"/>
/// on the current target.
/// </para>
///
/// <para>Required BB keys: <see cref="BBDataSig.IntentSource"/>, <see cref="BBDataSig.Agent"/>.</para>
/// </summary>
[GlobalClass]
public partial class InteractorComponent3D : Area3D, IComponent
{
    [Export, RequiredExport] public InputAction InteractAction { get; private set; } = null!;

    /// <summary>The nearest in-range interactable that currently passes CanInteract, or null.</summary>
    public IInteractable3D? CurrentTarget { get; private set; }

    /// <summary>Fired whenever <see cref="CurrentTarget"/> changes (including to null).</summary>
    public event Action<IInteractable3D?> TargetChanged = delegate { };

    /// <summary>Fired when the interact action dispatches to the current target.</summary>
    public event Action<IInteractable3D> Interacted = delegate { };

    private IBlackboard _bb = null!;
    private IIntentSource _intentSource = null!;
    private Node3D _agent = null!;

    private readonly List<IInteractable3D> _interactablesInRange = new();
    private readonly List<IInteractable3D> _eligibleScratch = new();
    private readonly List<Vector3> _positionScratch = new();
    private bool _wasPressedLastFrame;
    private bool _dispatchingTargetChange;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _ExitTree()
    {
        AreaEntered -= OnAreaEntered;
        AreaExited -= OnAreaExited;
    }

    public override void _Process(double delta)
    {
        RecomputeTarget();

        (CurrentTarget as IInteractionFeedbackProvider3D)?.FeedbackStrategy?.OnProcess(delta);

        var intents = _intentSource.GetProcessIntents();
        bool isPressed = intents.TryGetValue(InteractAction, out var data) && data.GetBool();
        bool justPressed = isPressed && !_wasPressedLastFrame;
        _wasPressedLastFrame = isPressed;

        if (!justPressed || CurrentTarget == null) { return; }

        var target = CurrentTarget;
        target.Interact(_agent);
        Interacted.Invoke(target);
        JmoLogger.Info(this, $"[Interaction] dispatched interact to {(target as Node)?.Name}");
    }

    private void RecomputeTarget()
    {
        _interactablesInRange.RemoveAll(i => i is not Node3D node || !IsInstanceValid(node));

        _eligibleScratch.Clear();
        _positionScratch.Clear();
        foreach (var interactable in _interactablesInRange)
        {
            if (!interactable.CanInteract(_agent)) { continue; }
            _eligibleScratch.Add(interactable);
            _positionScratch.Add(((Node3D)interactable).GlobalPosition);
        }

        int idx = JmoMath.SelectNearest(_positionScratch, GlobalPosition);
        SetCurrentTarget(idx < 0 ? null : _eligibleScratch[idx]);
    }

    private void SetCurrentTarget(IInteractable3D? newTarget)
    {
        if (CurrentTarget == newTarget) { return; }
        // C# events fire synchronously; a subscriber that triggers another recompute
        // would re-enter here. Guard so a target change can't recurse into itself.
        if (_dispatchingTargetChange) { return; }

        _dispatchingTargetChange = true;
        try
        {
            var previous = CurrentTarget;
            CurrentTarget = newTarget;

            (previous as IInteractionFeedbackProvider3D)?.FeedbackStrategy?.OnUntargeted();

            if (newTarget is IInteractionFeedbackProvider3D provider && provider.FeedbackStrategy != null)
            {
                var ctx = new InteractionFeedbackContext(_agent, (Node3D)newTarget, InteractAction);
                provider.FeedbackStrategy.OnTargeted(ctx);
            }

            TargetChanged.Invoke(newTarget);
        }
        finally
        {
            _dispatchingTargetChange = false;
        }
    }

    private void OnAreaEntered(Area3D area)
    {
        if (area is IInteractable3D interactable && !_interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Add(interactable);
        }
    }

    private void OnAreaExited(Area3D area)
    {
        if (area is IInteractable3D interactable)
        {
            _interactablesInRange.Remove(interactable);
        }
    }

    #region IComponent

    public bool IsInitialized { get; private set; }

    public bool Initialize(IBlackboard bb)
    {
        _bb = bb;
        if (!bb.TryGet<IIntentSource>(BBDataSig.IntentSource, out _intentSource))
        {
            JmoLogger.Error(this, "[Interaction] IntentSource missing from Blackboard — interaction dispatch is inert.");
            return false;
        }
        if (!bb.TryGet<Node3D>(BBDataSig.Agent, out _agent))
        {
            JmoLogger.Error(this, "[Interaction] Agent missing from Blackboard — cannot resolve interactor.");
            return false;
        }

        IsInitialized = true;
        Initialized.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;
        AreaEntered += OnAreaEntered;
        AreaExited += OnAreaExited;
    }

    public event Action Initialized = delegate { };

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Test Helpers
#if TOOLS

    internal void SetInteractAction(InputAction value) => InteractAction = value;
    internal void SimulateAreaEntered(Area3D area) => OnAreaEntered(area);
    internal void SimulateAreaExited(Area3D area) => OnAreaExited(area);

#endif
    #endregion
}
