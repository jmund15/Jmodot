namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections;
using System.Collections.Generic;
using Core.AI.Perception;

/// <summary>
/// The active memories of one category. A <c>foreach</c> binds to the struct enumerator below, so
/// the steering hot path walks a category without allocating; LINQ callers still see an
/// <see cref="IEnumerable{T}"/>. Valid until the manager's memories next change.
/// </summary>
public readonly struct CategoryMemories : IEnumerable<Perception3DInfo>
{
    private readonly HashSet<Perception3DInfo>? _memories;

    internal CategoryMemories(HashSet<Perception3DInfo>? memories) => _memories = memories;

    public Enumerator GetEnumerator() => new(_memories);

    IEnumerator<Perception3DInfo> IEnumerable<Perception3DInfo>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<Perception3DInfo>
    {
        private HashSet<Perception3DInfo>.Enumerator _inner;
        private readonly bool _hasMemories;

        internal Enumerator(HashSet<Perception3DInfo>? memories)
        {
            _hasMemories = memories != null;
            _inner = memories != null ? memories.GetEnumerator() : default;
            Current = null!;
        }

        public Perception3DInfo Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (!_hasMemories) { return false; }
            while (_inner.MoveNext())
            {
                if (!_inner.Current.IsActive) { continue; }
                Current = _inner.Current;
                return true;
            }
            return false;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() => _inner.Dispose();
    }
}
