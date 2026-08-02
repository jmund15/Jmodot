namespace Jmodot.Core.Identification;

/// <summary>
///     Opt-in debug identity consulted by <c>JmoLogger</c> in preference to the node path.
/// </summary>
public interface ILogIdentity
{
    /// <summary>Gets the human-readable label this object is logged under. Empty falls back to the default format.</summary>
    string LogLabel { get; }
}
