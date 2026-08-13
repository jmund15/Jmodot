namespace Jmodot.Implementation.Shared;

using System;
using System.Collections.Generic;
using Godot;
using Jmodot.Core.Shared;

/// <summary>
///     Resolves an <see cref="ISceneSingleton{TSelf}" /> for a consumer, turning "the scene has none" from a silent
///     no-op into exactly one log line per (system, consumer type) pair.
/// </summary>
/// <remarks>
///     Absence is legitimate — a level with no wind hosts no wind system — so this returns null rather than throwing,
///     and callers still skip their work. What it removes is the failure mode where an UNINTENTIONALLY unplaced
///     system makes an entire subsystem invisible with a green build. Throttling is keyed by consumer TYPE, not
///     instance, so a per-frame consumer and a per-cast one each get one line instead of flooding the log.
/// </remarks>
public static class SingletonNodeResolver
{
    private static readonly HashSet<(Type System, Type Consumer)> WarnedPairs = new();

    /// <summary>
    ///     Return the live <typeparamref name="T" /> for the current scene, or null after warning once that the scene
    ///     hosts none.
    /// </summary>
    /// <param name="consumer">The object requesting the system; named in the warning and used as the log source.</param>
    public static T? ResolveOrWarn<T>(object consumer)
        where T : Node, ISceneSingleton<T>
    {
        var instance = T.Instance;
        if (instance != null)
        {
            return instance;
        }

        var consumerType = consumer.GetType();
        if (WarnedPairs.Add((typeof(T), consumerType)))
        {
            JmoLogger.Warning(consumer,
                $"{typeof(T).Name}.Instance is null — no {typeof(T).Name} is registered in the current scene, so " +
                $"work requested by '{consumerType.Name}' is being skipped. {T.AbsenceRemedy}");
        }

        return null;
    }

    /// <summary>Clear the warn-once ledger so a test run cannot swallow the warning a later test asserts on.</summary>
    internal static void Reset()
    {
        WarnedPairs.Clear();
    }
}
