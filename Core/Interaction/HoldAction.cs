namespace Jmodot.Core.Interaction;

/// <summary>
/// How an <see cref="IHolder3D"/> physically attaches a held node.
/// <c>Nothing</c> = logical hold only; <c>AddChild</c> = parent under the holder;
/// <c>Reparent</c> = full reparent with transform reset.
/// </summary>
public enum HoldAction
{
    Nothing,
    AddChild,
    Reparent
}
