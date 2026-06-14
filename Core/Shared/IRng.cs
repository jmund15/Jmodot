namespace Jmodot.Core.Shared;

/// <summary>
///     Engine-agnostic seeded random-number contract: the scalar draws the procedural-generation
///     kernel consumes. Deliberately Godot-free (no <c>Vector2/3</c> direction helpers — those return
///     <c>Godot</c> types and stay concrete on <see cref="Jmodot.Implementation.Shared.JmoRng" />) so a
///     future pure-CLR implementation (e.g. an xorshift PRNG) can satisfy this interface with no engine
///     dependency, letting the generator run headless.
///     <para>
///         The generator depends on this interface through an injected <c>Func&lt;int, IRng&gt;</c>
///         factory rather than a concrete type, so swapping <c>JmoRng</c> for an engine-free RNG is a
///         one-line change at the call site. <c>JmoRng</c> is the production implementation.
///     </para>
/// </summary>
public interface IRng
{
    /// <summary>Returns a random float in [0, 1).</summary>
    float GetRndFloat();

    /// <summary>Returns a random int in [0, <paramref name="max" />). For array indexing.</summary>
    int GetRndInt(int max);

    /// <summary>Returns a random int in [<paramref name="min" />, <paramref name="max" />] (inclusive both ends).</summary>
    int GetRndInRange(int min, int max);

    /// <summary>Returns a random float in [<paramref name="min" />, <paramref name="max" />) (max exclusive).</summary>
    float GetRndInRange(float min, float max);

    /// <summary>Returns a deterministic random <c>long</c> in [0, <paramref name="maxExclusive" />).</summary>
    long GetRndLong(long maxExclusive);

    /// <summary>Returns +1f or -1f randomly.</summary>
    float GetRndSign();
}
