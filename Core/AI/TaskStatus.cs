namespace Jmodot.Core.AI;

/// <summary>
/// Represents the execution status of a Behavior Tree task.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// The task has not been evaluated yet or has been reset after completion.
    /// </summary>
    FRESH,
    /// <summary>
    /// The task is currently executing and has not yet finished.
    /// </summary>
    RUNNING,
    /// <summary>
    /// The task has completed its execution successfully.
    /// </summary>
    SUCCESS,
    /// <summary>
    /// The task has failed to complete its execution.
    /// </summary>
    FAILURE
}
