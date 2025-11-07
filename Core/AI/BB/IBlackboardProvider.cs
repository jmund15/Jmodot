namespace Jmodot.Core.AI.BB;

/// <summary>
/// Optional interface for objects that can automatically register themselves
/// with a blackboard during initialization.
/// </summary>
public interface IBlackboardProvider
{
    /// <summary>
    /// The Key-Value pair this component provides to the blackboard.
    /// Return null to opt-out of automatic registration.
    /// The Key is typically a constant from a class like BBDataSig.
    /// The Value is almost always 'this'.
    /// </summary>
    (StringName Key, object Value)? Provision { get; }
}
