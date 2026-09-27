namespace Jmodot.Core.Shared.Attributes;

using System;

/// <summary>
/// Marks an [Export] property or field as required.
/// Use with <c>this.ValidateRequiredExports()</c> to fail-fast
/// with a clear error message if any required exports are not assigned.
/// Supported on both <see cref="Godot.Node"/> (via NodeExts) and <see cref="Godot.Resource"/> (via ResourceExts).
/// Members declared on base classes are checked too, private ones included; an unassigned member
/// also reaches the editor dock wherever the host appends
/// <see cref="Jmodot.Implementation.Shared.ConfigWarnings.RequiredExports"/>.
/// </summary>
/// <remarks>
/// Node usage:
/// <code>
/// [Export, RequiredExport] public SpellArchetype Archetype { get; set; } = null!;
///
/// public override void _Ready()
/// {
///     this.ValidateRequiredExports();
/// }
/// </code>
/// Resource usage:
/// <code>
/// [Export, RequiredExport] public Resource Config { get; set; } = null!;
///
/// // Call during initialization
/// this.ValidateRequiredExports();
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class RequiredExportAttribute : Attribute
{
    /// <param name="consequence">
    /// Optional sentence telling the designer what breaks while the member is unassigned; the dock
    /// warning prints it after the member's Inspector name.
    /// </param>
    public RequiredExportAttribute(string? consequence = null)
    {
        Consequence = consequence;
    }

    /// <summary>What breaks while the member is unassigned, or null when the name says enough.</summary>
    public string? Consequence { get; }
}
