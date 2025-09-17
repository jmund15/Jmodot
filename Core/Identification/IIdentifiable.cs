namespace Jmodot.Core.Identification;

/// <summary>
///     A universal interface for any object that has a semantic identity. It provides
///     a standardized contract for querying an object's defining Identity resource.
/// </summary>
/// <remarks>
///     This allows any system (like AI Perception) to query an object for what it is and
///     what categories it belongs to, regardless of its specific type.
/// </remarks>
public interface IIdentifiable
{
    /// <summary>Gets the Identity resource that defines this object's type and categories.</summary>
    Identity GetIdentity();
}
