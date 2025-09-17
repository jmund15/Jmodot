namespace Jmodot.Core.Shared;

/// <summary>
///     A standardized interface ensuring any component that is a Node
///     can easily return a reference to itself. Useful for clean dependency management.
/// </summary>
public interface IGodotNodeInterface
{
    /// <summary>
    ///     Gets the Godot.Node instance that implements this interface.
    /// </summary>
    /// <returns>The implementing Node itself.</returns>
    public Node GetInterfaceNode();
}