namespace Jmodot.Implementation.AI.Perception;

using Godot;
using Core.Identification;
using Shared;

/// <summary>
/// The one rule every sensor in this family perceives a body through: the identity source is the body itself,
/// else its first <see cref="IIdentifiable"/> child. A body with neither is never perceived.
/// </summary>
internal static class SensorIdentity
{
    internal static bool TryResolve(Node3D body, out IIdentifiable? identifiable)
    {
        if (body is IIdentifiable directId)
        {
            identifiable = directId;
            return true;
        }

        return body.TryGetFirstChildOfInterface(out identifiable);
    }
}
