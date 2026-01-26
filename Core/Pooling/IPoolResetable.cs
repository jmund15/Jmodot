namespace Jmodot.Core.Pooling;

/// <summary>
/// Interface for components that need to reset their state when their parent entity
/// returns to an object pool. Enables automatic discovery and cleanup of poolable components.
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage Pattern:</b>
/// Components implement this interface and define their own cleanup logic in <c>OnPoolReset()</c>.
/// The parent entity (e.g., CharacterScene, RigidBodyScene) uses <c>GetChildrenOfInterface&lt;IPoolResetable&gt;()</c>
/// to discover and reset all components automatically, eliminating manual reset call management.
/// </para>
/// <para>
/// <b>What to Reset:</b>
/// <list type="bullet">
/// <item>Subscriptions/event handlers that could leak across pool cycles</item>
/// <item>Cached references to external objects (providers, areas, targets)</item>
/// <item>Internal state that must be fresh for each spell lifetime</item>
/// </list>
/// </para>
/// <para>
/// <b>What NOT to Reset:</b>
/// <list type="bullet">
/// <item>Configuration exports (these persist correctly)</item>
/// <item>Signal connections (established once in _Ready(), persist across pool cycles)</item>
/// <item>Internal child components (handled by their own IPoolResetable implementation)</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyComponent : Node, IPoolResetable
/// {
///     private HashSet&lt;IProvider&gt; _activeProviders = new();
///
///     public void OnPoolReset()
///     {
///         // Clear external references that could leak
///         _activeProviders.Clear();
///     }
/// }
/// </code>
/// </example>
public interface IPoolResetable
{
    /// <summary>
    /// Reset component state when parent entity returns to pool.
    /// Called automatically by parent's ResetForPool() via GetChildrenOfInterface.
    /// </summary>
    void OnPoolReset();
}
