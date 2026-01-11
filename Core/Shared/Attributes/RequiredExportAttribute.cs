namespace Jmodot.Core.Shared.Attributes;

using System;

/// <summary>
/// Marks an [Export] property or field as required.
/// Use with <c>this.ValidateRequiredExports()</c> in _Ready() to fail-fast
/// with a clear error message if any required exports are not assigned.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// [Export, RequiredExport] public SpellArchetype Archetype { get; set; } = null!;
///
/// public override void _Ready()
/// {
///     this.ValidateRequiredExports();
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class RequiredExportAttribute : Attribute
{
}
