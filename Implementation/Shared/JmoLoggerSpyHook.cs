namespace Jmodot.Implementation.Shared;

#if TOOLS
using System;

/// <summary>
/// Internal-visibility bridge to JmoLogger's #if TOOLS test-spy event.
/// Lives inside Jmodot.Implementation.Shared so it has internal access to the
/// _TestOnLogEmitted event; called from the PP test layer via friend-internal
/// resolution (single-csproj sharing).
/// </summary>
internal static class JmoLoggerSpyHook
{
    public static void Subscribe(Action<LogLevel, string, string> handler)
    {
        JmoLogger._TestOnLogEmitted += handler;
    }

    public static void Unsubscribe(Action<LogLevel, string, string> handler)
    {
        JmoLogger._TestOnLogEmitted -= handler;
    }
}
#endif
