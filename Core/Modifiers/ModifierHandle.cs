namespace Jmodot.Core.Modifiers;

using System;

/// <summary>
/// An opaque token representing a single, specific application of a stat modifier.
/// This handle is returned by IStatProvider.TryAddModifier and is used for the precise
/// removal of that one modifier instance, even if the same modifier is applied multiple times.
/// The client should not need to know or care about its internal contents.
/// </summary>
public sealed class ModifierHandle
{
    internal IModifiableProperty Property { get; }
    internal Guid ModifierId { get; }

    internal ModifierHandle(IModifiableProperty property, Guid modifierId)
    {
        Property = property;
        ModifierId = modifierId;
    }
}

