namespace Jmodot.Core.Profiling;

using System;
using System.Collections.Generic;

/// <summary>
/// Caches the result of allocating a native handle for a string, returning the same handle for
/// repeated strings. Separates the intern <em>policy</em> from the allocation <em>mechanism</em>
/// (the injected <paramref name="alloc"/> delegate) so the policy is pure-CLR testable while the
/// real allocation (e.g. <c>CString.FromString</c>) stays an untested adapter detail.
///
/// <para>Tracy stores plot / frame-mark name pointers <b>by reference, not by copy</b>, so the
/// allocated handle must outlive every event that uses it. This interner never evicts — each
/// unique string allocates exactly once and is retained for the process lifetime, which is the
/// correct behavior for that pointer-retention contract.</para>
/// </summary>
public sealed class NativeStringInterner<T>
{
    private readonly Func<string, T> _alloc;
    private readonly Dictionary<string, T> _cache = new();

    public NativeStringInterner(Func<string, T> alloc)
    {
        this._alloc = alloc ?? throw new ArgumentNullException(nameof(alloc));
    }

    /// <summary>Number of distinct strings interned so far. Test observability only.</summary>
    public int Count => this._cache.Count;

    /// <summary>Returns the cached handle for <paramref name="s"/>, allocating once on first sight.</summary>
    public T Intern(string s)
    {
        if (this._cache.TryGetValue(s, out var existing))
        {
            return existing;
        }

        var allocated = this._alloc(s);
        this._cache[s] = allocated;
        return allocated;
    }
}
