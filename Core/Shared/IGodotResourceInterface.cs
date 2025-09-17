namespace Jmodot.Core.Shared;

public interface IGodotResourceInterface
{
    /// <summary>
    ///     Gets the Godot.Resource instance that implements this interface.
    /// </summary>
    /// <returns>The implementing Resource itself.</returns>
    Resource GetUnderlyingResource();
}
