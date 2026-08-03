namespace Jmodot.Core.Interaction;

/// <summary>
/// How far along an attachment is. Booking capacity and arriving are separate events: a rider
/// reserves its anchor before it starts flying, so for the whole approach the host holds a record
/// for a rider that is not yet touching it.
/// </summary>
public enum AttachmentPhase
{
    /// <summary>
    /// Capacity and an anchor are booked, but the rider is still travelling. It grips nothing — a
    /// shed spends no force against it — and it is not riding for any purpose that asks.
    /// </summary>
    Reserved,

    /// <summary>The rider arrived and the host confirmed it. Grip, ride damage and shedding all apply.</summary>
    Riding,
}
