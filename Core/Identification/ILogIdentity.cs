namespace Jmodot.Core.Identification;

/// <summary>
///     Opt-in debug identity consulted by <see cref="Jmodot.Implementation.Shared.JmoLogger"/> in preference to the node path.
/// </summary>
public interface ILogIdentity
{
    /// <summary>Gets the human-readable label this object is logged under. Empty falls back to the default format.</summary>
    string LogLabel { get; }
}
