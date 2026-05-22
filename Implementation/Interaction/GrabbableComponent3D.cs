namespace Jmodot.Implementation.Interaction;

using System;
using Godot;
using Jmodot.Core.Interaction;
using Jmodot.Implementation.Shared;

/// <summary>
/// Reusable capability for an object that can be grabbed, held, dropped, and thrown.
/// Manages its own internal mechanical state for a robust reserve→confirm→cancel handshake
/// and the physical attachment/detachment of its owner. Agnostic of any owner gameplay logic
/// (HSMs). The grab handshake is keyed on <see cref="IHolder3D"/>, so any holder can drive it.
/// Target highlighting is the interactor's concern (driven by its TargetChanged event), not this
/// component's.
/// </summary>
[GlobalClass]
public partial class GrabbableComponent3D : Node3D, IGrabbable3D, IThrowable3D, IDroppable3D
{
    /// <summary>
    /// Low-level mechanical state, distinct from any high-level behavioral state the parent
    /// entity might have (e.g. Idle, Cooking).
    /// </summary>
    private enum MechanicalState { AVAILABLE, RESERVED, HELD }
    private MechanicalState _currentState = MechanicalState.AVAILABLE;

    // The holder that reserved this object but has not yet confirmed the grab.
    private IHolder3D? _reservingHolder;
    // The holder currently holding this object.
    private IHolder3D? _currentHolder;

    public Node3D PhysicalBody { get; private set; } = null!;
    // The owner's original parent, to drop it back correctly.
    private Node _originalParent = null!;

    #region Events
    public event Action<Node3D> OnGrabbed = delegate { };
    public event Action<Node3D> OnDropped = delegate { };
    public event Action<Node3D, Vector3> OnThrown = delegate { };
    public event Action<Node3D> OnReleased = delegate { };
    #endregion

    #region Public State Control API (For the Owner)
    public bool IsHeld => _currentState == MechanicalState.HELD;

    public bool IsGrabbable { get; private set; } = true;

    /// <inheritdoc />
    public HoldAction PreferredHoldAction => HoldAction.Reparent;

    public void DisableGrabbing() => IsGrabbable = false;
    public void EnableGrabbing() => IsGrabbable = true;
    #endregion

    public override void _Ready()
    {
        // This component assumes it is a direct child of the entity's root node.
        PhysicalBody = GetParent<Node3D>();
        if (PhysicalBody == null)
        {
            JmoLogger.Error(this, "GrabbableComponent must have a Node3D parent to manipulate!");
            QueueFree();
        }
    }

    #region IInteractable3D
    // The grabbable is "interactable" only in the capability-query sense — the real grab
    // path is the IHolder3D handshake (RequestGrab/ConfirmGrab). Generic Interact has no
    // holder context, so it is a no-op; grabbing flows through RequestGrab(IHolder3D).
    public bool CanInteract(Node3D interactor) => IsGrabbable;
    public void Interact(Node3D interactor) { }
    #endregion

    #region Transactional Handshake

    /// <summary>
    /// Step 1: a holder requests to grab this object. If available, the object is reserved,
    /// preventing other holders from interfering while the grab animation plays out.
    /// </summary>
    public bool RequestGrab(IHolder3D holder)
    {
        if (!IsGrabbable || _currentState != MechanicalState.AVAILABLE)
        {
            return false;
        }

        _currentState = MechanicalState.RESERVED;
        _reservingHolder = holder;
        return true;
    }

    /// <summary>
    /// Step 2a (Success): the reserving holder confirms the grab, finalizing the transaction.
    /// Typically called from an animation keyframe.
    /// </summary>
    public void ConfirmGrab(IHolder3D holder)
    {
        // Only the holder that reserved the item may confirm.
        if (_currentState != MechanicalState.RESERVED || _reservingHolder != holder)
        {
            return;
        }

        _currentState = MechanicalState.HELD;
        _currentHolder = holder;
        _reservingHolder = null;

        _originalParent = PhysicalBody.GetParent();

        if (PhysicalBody is RigidBody3D rb)
        {
            rb.Freeze = true;
        }

        OnGrabbed?.Invoke((Node3D)holder.GetUnderlyingNode());
    }

    /// <summary>
    /// Step 2b (Failure): the reserving holder cancels, releasing the reservation
    /// (e.g. the holder was interrupted during its grab animation).
    /// </summary>
    public void CancelGrab(IHolder3D holder)
    {
        if (_currentState == MechanicalState.RESERVED && _reservingHolder == holder)
        {
            _currentState = MechanicalState.AVAILABLE;
            _reservingHolder = null;
        }
    }
    #endregion

    #region Release Actions

    /// <summary>
    /// Grabbable taken over by another state/logic/component (e.g. Ingredient).
    /// </summary>
    public void RelinquishControl()
    {
        if (_currentState != MechanicalState.HELD || _currentHolder == null)
        {
            return;
        }
        var holder = _currentHolder;
        _currentHolder = null;
        _currentState = MechanicalState.AVAILABLE;
        OnReleased?.Invoke((Node3D)holder.GetUnderlyingNode());
    }

    public void Drop()
    {
        ReleaseInternal(fireDropped: true, fireReleased: true);
    }

    public void Throw(Vector3 throwVelocity)
    {
        if (_currentState != MechanicalState.HELD)
        {
            JmoLogger.Warning(this, $"Throw called but state is {_currentState}, not HELD — skipping!");
            return;
        }

        var holder = _currentHolder;
        // ReleaseInternal handles state cleanup and fires OnDropped + OnReleased once.
        ReleaseInternal(fireDropped: true, fireReleased: true);

        // holder guaranteed non-null when we were in HELD state.
        OnThrown?.Invoke((Node3D)holder!.GetUnderlyingNode(), throwVelocity);
    }

    /// <summary>
    /// Shared cleanup for Drop and Throw. Centralizes state reset and event firing
    /// to prevent double OnReleased invocations.
    /// </summary>
    private void ReleaseInternal(bool fireDropped, bool fireReleased)
    {
        if (_currentState != MechanicalState.HELD)
        {
            return;
        }

        var holder = _currentHolder;
        _currentHolder = null;
        _currentState = MechanicalState.AVAILABLE;

        if (PhysicalBody is RigidBody3D rb) { rb.Freeze = false; }

        var holderNode = (Node3D)holder!.GetUnderlyingNode();
        if (fireDropped) { OnDropped?.Invoke(holderNode); }
        if (fireReleased) { OnReleased?.Invoke(holderNode); }
    }
    #endregion

    public Node GetUnderlyingNode() => PhysicalBody;
}
