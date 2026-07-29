namespace Jmodot.Core.AI.Navigation.Considerations;

/// <summary>
/// Per-agent mutable state for one steering consideration, owned by the per-instance
/// <c>AISteeringProcessor3D</c>. Consideration Resources themselves hold zero per-agent state, so
/// every value that varies between two agents sharing a <c>.tres</c> lives on a subclass of this type.
/// </summary>
public abstract class AIConsiderationRuntime
{
}
