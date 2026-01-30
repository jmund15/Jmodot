namespace Jmodot.Core.Shared.Attributes;

using System;

/// <summary>
/// Marks an [Export] property or field as required.
/// Use with <c>this.ValidateRequiredExports()</c> to fail-fast
/// with a clear error message if any required exports are not assigned.
/// Supported on both <see cref="Godot.Node"/> (via NodeExts) and <see cref="Godot.Resource"/> (via ResourceExts).
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
}
