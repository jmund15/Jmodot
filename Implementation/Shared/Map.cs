namespace Jmodot.Implementation.Shared;

using System.Collections;
using System.Collections.Generic;

public class Map<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
{
    private readonly Dictionary<T1, T2> _forward = new();
    private readonly Dictionary<T2, T1> _reverse = new();

    public Map()
    {
        this.Forward = new Indexer<T1, T2>(this._forward);
        this.Reverse = new Indexer<T2, T1>(this._reverse);
    }

    public Indexer<T1, T2> Forward { get; private set; }
    public Indexer<T2, T1> Reverse { get; private set; }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
    {
        return this._forward.GetEnumerator();
    }

    public void Add(T1 t1, T2 t2)
    {
        this._forward.Add(t1, t2);
        this._reverse.Add(t2, t1);
    }

    public void Remove(T1 t1)
    {
        var revKey = this.Forward[t1];
        this._forward.Remove(t1);
        this._reverse.Remove(revKey);
    }

    public void Remove(T2 t2)
    {
        var forwardKey = this.Reverse[t2];
        this._reverse.Remove(t2);
        this._forward.Remove(forwardKey);
    }

    public void Clear()
    {
        _forward.Clear();
        _reverse.Clear();
    }
    public class Indexer<T3, T4>
    {
        private readonly Dictionary<T3, T4> _dictionary;

        public Indexer(Dictionary<T3, T4> dictionary)
        {
            this._dictionary = dictionary;
        }

        public T4 this[T3 index]
        {
            get => this._dictionary[index];
            set => this._dictionary[index] = value;
        }

        public bool Contains(T3 key)
        {
            return this._dictionary.ContainsKey(key);
        }
    }
}
