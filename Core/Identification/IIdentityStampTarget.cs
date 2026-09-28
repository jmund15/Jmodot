namespace Jmodot.Core.Identification;

using System.Collections.Generic;

/// <summary>
/// Capability query: this entity's expressed identity may be composed at runtime from an authored
/// base plus keyed category contributions. Contributors ask for this interface rather than
/// type-testing a concrete entity class.
/// </summary>
/// <remarks>
/// Implementers hold an <see cref="IdentityStampComposer"/> and forward to it, keeping their
/// authored identity as the pristine clone source: <see cref="IIdentifiable.GetIdentity"/> serves
/// the composed identity while one exists and the authored base otherwise, so clearing every stamp
/// restores the authored instance by reference.
/// An implementer that replaces its authored base identity at runtime must recompose the composer
/// against the new base (e.g. <c>ClearStamp(self, newBase)</c> with a never-stamped key), or
/// <see cref="IIdentifiable.GetIdentity"/> keeps serving a clone composed over the superseded base.
/// </remarks>
public interface IIdentityStampTarget : IIdentifiable
{
    /// <summary>Registers (or replaces) one owner's category contribution. <paramref name="key"/>
    /// identifies the owner and is compared by reference.</summary>
    void StampCategories(object key, IReadOnlyList<Category> categories);

    /// <summary>Retracts one owner's contribution. Unknown keys are a no-op.</summary>
    void ClearStamp(object key);
}
