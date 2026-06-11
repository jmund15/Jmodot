namespace Jmodot.Implementation.Shared;

using System.Globalization;

/// <summary>
/// A per-lineage counter that derives a fresh child seed on each <see cref="Next"/>,
/// folding its running <see cref="Count"/> as an invariant-culture STRING segment
/// through <see cref="SeedManager.DeriveChild(int, string[])"/>. This is the
/// string-counter lineage domain — deliberately distinct from the int-segment
/// hit-path overloads, which do NOT compose with it (see <see cref="SeedManager"/>).
/// <para>
/// Declared a sealed CLASS, never a struct: instances are shared by reference (e.g.
/// stored in a <c>Dictionary&lt;archetype, SeedSequence&gt;</c>), and a struct copy
/// returned by the indexer would never advance the stored counter — every lookup
/// would re-derive the same seed.
/// </para>
/// </summary>
public sealed class SeedSequence
{
    private readonly int _parentSeed;
    private readonly string _label;
    private int _count;

    /// <summary>
    /// Create a sequence rooted at <paramref name="parentSeed"/> under
    /// <paramref name="label"/>; the counter starts at zero. Each <see cref="Next"/>
    /// folds the label then the current count, so distinct labels under the same parent
    /// derive disjoint streams.
    /// </summary>
    public SeedSequence(int parentSeed, string label)
    {
        this._parentSeed = parentSeed;
        this._label = label;
    }

    /// <summary>How many times <see cref="Next"/> has been called. Save-serialization read seam.</summary>
    public int Count => this._count;

    /// <summary>
    /// Restore the counter to <paramref name="count"/> so the next <see cref="Next"/>
    /// derives the same seed a fresh sequence would after advancing that many times.
    /// Save/resume seam — called post-Bind during load.
    /// </summary>
    public void InjectCount(int count) => this._count = count;

    /// <summary>
    /// Derive the next child seed and advance the counter. Folds the current
    /// <see cref="Count"/> as an invariant-culture string segment via the string
    /// overload of <see cref="SeedManager.DeriveChild(int, string[])"/>.
    /// </summary>
    public int Next()
    {
        int derived = SeedManager.DeriveChild(
            this._parentSeed, this._label, this._count.ToString(CultureInfo.InvariantCulture));
        this._count++;
        return derived;
    }
}
