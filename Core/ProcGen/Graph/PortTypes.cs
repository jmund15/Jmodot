namespace Jmodot.Core.ProcGen.Graph;

using Godot;

/// <summary>
///     The kernel's single port-type compatibility predicate: equal types match, and an
///     unset/empty <see cref="StringName" /> on either side is the wildcard that matches any
///     type. Extracted as the one source of truth so the generator's placement passes and the
///     spatial <c>PortCompatibility</c> layer both delegate here rather than re-deriving the rule.
/// </summary>
public static class PortTypes
{
    public static bool Matches(StringName portType, StringName requiredType)
    {
        bool portWildcard = string.IsNullOrEmpty(portType);
        bool requiredWildcard = string.IsNullOrEmpty(requiredType);
        return portWildcard || requiredWildcard || portType == requiredType;
    }
}
