namespace Jmodot.Core.Actors;

/// <summary>
/// Dimension-neutral contract behind the shared <c>BBDataSig.ExternalForceReceiver</c> key.
/// The 2D and 3D receivers publish under one key so consumers address the concept rather than
/// the dimension; without this contract a consumer resolving that key by concrete type silently
/// gets nothing from the other dimension's receiver, and the sustained-force attribution and
/// control-loss paths degrade with no log.
/// </summary>
/// <remarks>
/// Deliberately excluded because they cannot be expressed dimension-neutrally over the current
/// provider interfaces: internal-provider registration (<c>IForceProvider2D</c> and
/// <c>IForceProvider3D</c> share no base type), the vector-valued force/offset queries, and the
/// velocity-offset capture split (<c>IForceProvider2D</c> carries no <c>IsCaptureForce</c>
/// concept at all, so a 2D receiver reports zero capture force by construction). Consumers that
/// need those work against the concrete receiver.
/// </remarks>
public interface IExternalForceReceiver
{
    /// <summary>
    /// Magnitude of the aggregated capture-tagged force on <paramref name="target"/> — the
    /// control-loss quantity. Zero when the target is not of this receiver's dimension.
    /// </summary>
    float GetCaptureForceMagnitude(Node target);

    /// <summary>
    /// Magnitude of the aggregated force from every active provider, capture-tagged or not.
    /// Zero when the target is not of this receiver's dimension.
    /// </summary>
    float GetTotalForceMagnitude(Node target);

    /// <summary>
    /// The provider node contributing the strongest force to <paramref name="target"/>, or null
    /// when no provider is active. Used for sustained-force damage attribution.
    /// </summary>
    Node? GetDominantForceSourceNode(Node target);
}
