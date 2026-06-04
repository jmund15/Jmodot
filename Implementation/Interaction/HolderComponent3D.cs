namespace Jmodot.Implementation.Interaction;

using System;
using System.Collections.Generic;
using Core.AI.BB;
using Core.Components;
using Core.Input;
using Core.Interaction;
using Core.Shared.Attributes;
using Core.Stats;
using Implementation.AI.BB;
using Implementation.Shared;
using GCol = Godot.Collections;

/// <summary>
/// Actor-side holder: drives the reserve→confirm grab handshake against an
/// <see cref="IGrabbable3D"/>, physically holds the grabbed body (reparent), and offers a
/// modular set of <see cref="ReleaseVerb"/> affordances (throw/drop/…) selected by input.
/// Framework-general — the consuming game's animation state machine calls <see cref="ConfirmHold"/>
/// from a reach keyframe and <see cref="CompleteRelease"/> after a release animation.
///
/// <para>Replaces the actor-side role of the consuming game's former grab god-component; absorbs
/// its handshake + the hand's reparent-holding. Detection/dispatch is the
/// <see cref="InteractorComponent3D"/>'s job — grab arrives via the interact verb
/// (<see cref="GrabbableComponent3D.Interact"/> → <see cref="RequestHold"/>).</para>
///
/// <para>Required BB key: <see cref="BBDataSig.IntentSource"/>.
/// P2 scope: events are consumed only by tests — no external <c>GodotObject</c> subscriber exists
/// yet, so no <c>_ExitTree</c> teardown is required. Re-audit at P4 when the wizard HandSM subscribes.</para>
/// </summary>
[GlobalClass]
public partial class HolderComponent3D : Node3D, IComponent, IHolder3D
{
    /// <summary>Modular release affordances. Empty = a hold-only holder (valid).</summary>
    [Export] public GCol.Array<ReleaseVerb> ReleaseVerbs { get; private set; } = new();

    /// <summary>Optional Vector2 aim action driving the held item's facing via <see cref="SetDirection"/>.</summary>
    [Export] public InputAction? AimAction { get; private set; }

    /// <summary>Reparent destination for held bodies. Falls back to this node.</summary>
    [Export] public Node3D? HoldAnchor { get; private set; }

    public Node3D? HeldNode { get; private set; }

    private IStatProvider? _statProvider;
    /// <inheritdoc />
    public IStatProvider? StatProvider => _statProvider;

    public event Action<IGrabbable3D> AttemptingGrab = delegate { };
    public event Action<IGrabbable3D> CompletedGrab = delegate { };

    /// <summary>Fired when a release verb's trigger is detected. The HandSM inspects the verb to
    /// pick the matching release animation; <see cref="CompleteRelease"/> applies the verb after.</summary>
    public event Action<ReleaseVerb, IGrabbable3D> AttemptingRelease = delegate { };

    private IBlackboard _bb = null!;
    private IIntentSource _intentSource = null!;

    private IGrabbable3D? _heldGrabbable;
    private IGrabbable3D? _reservedGrabbable;
    private ReleaseVerb? _pendingReleaseVerb;

    private Node? _heldNodeOGParent;
    private HoldAction _activeHoldAction;
    private Vector2 _holdingDirection = Vector2.Zero;
    private readonly Dictionary<ReleaseVerb, bool> _verbPressedLastFrame = new();

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        // Godot can serialize an [Export] collection as null (inspector-clear / .tres load),
        // overwriting the field initializer.
        if (ReleaseVerbs != null)
        {
            foreach (var verb in ReleaseVerbs)
            {
                verb?.ValidateRequiredExports();
            }
        }
        ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _Process(double delta)
    {
        var intents = _intentSource.GetProcessIntents();

        var matched = ProcessReleaseIntents(intents, _heldGrabbable);
        if (matched != null && _heldGrabbable != null)
        {
            _pendingReleaseVerb = matched;
            AttemptingRelease.Invoke(matched, _heldGrabbable);
        }

        if (AimAction != null
            && intents.TryGetValue(AimAction, out var aimData)
            && aimData.TryGetVector2(out var aim) && aim.HasValue
            && !aim.Value.IsZeroApprox())
        {
            SetDirection(aim.Value.Normalized());
        }
    }

    /// <summary>
    /// Pure verb-selection: returns the first verb whose trigger just became pressed (per-verb
    /// edge-tracked) and whose capability gate passes for the held grabbable, or null. Extracted
    /// from <see cref="_Process"/> so the decision is CLR-testable without a frame pump.
    /// </summary>
    internal ReleaseVerb? ProcessReleaseIntents(
        IReadOnlyDictionary<InputAction, IntentData> intents, IGrabbable3D? heldGrabbable)
    {
        if (heldGrabbable == null || ReleaseVerbs == null) { return null; }
        if (heldGrabbable.GetUnderlyingNode() is GodotObject go && !IsInstanceValid(go)) { return null; }

        foreach (var verb in ReleaseVerbs)
        {
            if (verb?.TriggerAction == null) { continue; }
            bool isPressed = intents.TryGetValue(verb.TriggerAction, out var data)
                && data.TryGetBool(out var b) && b == true;
            bool wasPressed = _verbPressedLastFrame.TryGetValue(verb, out var w) && w;
            _verbPressedLastFrame[verb] = isPressed;
            if (isPressed && !wasPressed && verb.CanRelease(heldGrabbable)) { return verb; }
        }
        return null;
    }

    #region Grab handshake / release lifecycle

    public bool RequestHold(IGrabbable3D grabbable)
    {
        if (grabbable == null || !grabbable.RequestGrab(this)) { return false; }
        _reservedGrabbable = grabbable;
        AttemptingGrab.Invoke(grabbable);
        return true;
    }

    /// <summary>Called by the consuming animation state machine from its reach keyframe to finalize the grab.</summary>
    public void ConfirmHold()
    {
        if (_reservedGrabbable == null) { return; }
        _reservedGrabbable.ConfirmGrab(this);
        _heldGrabbable = _reservedGrabbable;
        _reservedGrabbable = null;
        StartHolding(_heldGrabbable.PhysicalBody, _heldGrabbable.PreferredHoldAction);
        _bb.Set(BBDataSig.GrabberHolding, true);
        CompletedGrab.Invoke(_heldGrabbable);
    }

    /// <summary>Called if the reach animation is interrupted before confirmation.</summary>
    public void CancelHold()
    {
        if (_reservedGrabbable == null) { return; }
        _reservedGrabbable.CancelGrab(this);
        _reservedGrabbable = null;
    }

    /// <summary>Called by the consuming animation state machine after a release animation completes.</summary>
    public void CompleteRelease()
    {
        if (_pendingReleaseVerb == null || _heldGrabbable == null) { return; }

        // The grabbable may have been freed by another system between AttemptingRelease and now.
        if (_heldGrabbable.GetUnderlyingNode() is GodotObject go && !IsInstanceValid(go))
        {
            _heldGrabbable = null;
            _pendingReleaseVerb = null;
            HeldNode = null;
            _bb.Set(BBDataSig.GrabberHolding, false);
            return;
        }

        _pendingReleaseVerb.Release(this, _heldGrabbable);
        _heldGrabbable = null;
        _pendingReleaseVerb = null;
        _bb.Set(BBDataSig.GrabberHolding, false);
    }

    #endregion

    #region IHolder3D holding (self-contained reparent)

    public bool StartHolding(Node3D toHold, HoldAction action = HoldAction.Nothing)
    {
        if (HeldNode != null && !IsInstanceValid(HeldNode))
        {
            HeldNode = null;
            _heldNodeOGParent = null;
        }
        if (HeldNode != null) { return false; }

        _activeHoldAction = action;
        switch (action)
        {
            case HoldAction.AddChild:
                (HoldAnchor ?? this).AddChild(toHold);
                break;
            case HoldAction.Reparent:
                _heldNodeOGParent = toHold.GetParent();
                toHold.Reparent(HoldAnchor ?? this, false);
                toHold.Position = Vector3.Zero;
                break;
            case HoldAction.Nothing:
                break;
        }
        HeldNode = toHold;
        return true;
    }

    public bool StopHolding(Node3D? toStopHold)
    {
        if (HeldNode == null) { return false; }
        if (toStopHold != null && HeldNode != toStopHold) { return false; }

        if (IsInstanceValid(HeldNode) && _activeHoldAction is HoldAction.Reparent or HoldAction.AddChild)
        {
            var nodeToRelease = HeldNode;
            var dropPosition = nodeToRelease.GlobalPosition;
            nodeToRelease.Reparent(_heldNodeOGParent ?? GetTree().CurrentScene, false);
            nodeToRelease.GlobalPosition = dropPosition;
        }

        HeldNode = null;
        _heldNodeOGParent = null;
        _activeHoldAction = HoldAction.Nothing;
        return true;
    }

    public void SetDirection(Vector2 direction) => _holdingDirection = direction;
    public Vector2 GetDirection() => _holdingDirection;

    #endregion

    #region IComponent

    public bool IsInitialized { get; private set; }
    public event Action Initialized = delegate { };

    public bool Initialize(IBlackboard bb)
    {
        _bb = bb;
        if (!bb.TryGet<IIntentSource>(BBDataSig.IntentSource, out _intentSource))
        {
            JmoLogger.Error(this, "[Interaction] IntentSource missing from Blackboard — holder release is inert.");
            return false;
        }

        // Optional: a holder whose entity has no stats is valid (null StatProvider). Do NOT
        // early-return false on absence — read the framework interface, never a PP-concrete type.
        bb.TryGet<IStatProvider>(BBDataSig.Stats, out _statProvider);

        IsInitialized = true;
        Initialized.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        ProcessMode = ProcessModeEnum.Inherit;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region Test Helpers
#if TOOLS

    internal void SetReleaseVerbsForTest(GCol.Array<ReleaseVerb> verbs) => ReleaseVerbs = verbs;
    internal void SetAimActionForTest(InputAction action) => AimAction = action;

#endif
    #endregion
}
