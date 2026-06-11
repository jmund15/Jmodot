namespace Jmodot.Implementation.Shared;

using System;

/// <summary>
/// Declares the canonical, pinned seed-derivation key for a member of a seed-stream
/// or seed-kind registry. The <see cref="Key"/> — NOT the member's C# identifier
/// spelling — is what feeds seed derivation, so renaming or recasing the member never
/// silently shifts downstream seeds. A single reflective sweep can read this marker
/// across every registry to enforce cross-registry key uniqueness.
/// <para>
/// <see cref="AttributeTargets.Field"/> (enum members and <c>const</c> string fields
/// are both fields reflectively) makes misuse on any other target a compile error.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class SeedStreamKeyAttribute : Attribute
{
    public string Key { get; }

    public SeedStreamKeyAttribute(string key)
    {
        this.Key = key;
    }
}
