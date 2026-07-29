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
    /// <para>
    /// MUST be idempotent: it is evaluated more than once per entity lifetime (spell scenes run
    /// Phase 0 at _Ready and again at Initialize; pooled reuse re-runs it per activation), and the
    /// initializer re-reads it to retract the key when the publisher fails Phase 1. A lazily
    /// created payload must be cached on a field (<c>_x ??= new()</c>), never allocated fresh.
    /// </para>
    /// </summary>
    (StringName Key, object Value)? Provision { get; }
}
