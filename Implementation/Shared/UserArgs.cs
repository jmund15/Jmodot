namespace Jmodot.Implementation.Shared;

using System;

/// <summary>
/// Reads <c>key=value</c> user arguments, the ones passed after <c>--</c> on the engine command line.
/// Callers pass <see cref="Godot.OS.GetCmdlineUserArgs"/> in, so the parse stays pure.
/// </summary>
public static class UserArgs
{
    /// <summary>
    /// Finds the first argument of the form <c>key=value</c> whose key equals <paramref name="key"/> exactly.
    /// An argument without <c>=</c> never matches. On a miss <paramref name="value"/> is empty.
    /// </summary>
    public static bool TryGetValue(string[] args, string key, out string value)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (var arg in args)
        {
            if (arg.Length > key.Length && arg[key.Length] == '=' && arg.StartsWith(key, StringComparison.Ordinal))
            {
                value = arg[(key.Length + 1)..];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
