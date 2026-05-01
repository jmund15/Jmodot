namespace Jmodot.Core.Stats;

using Godot;
using Shared.Attributes;

/// <summary>
/// Pairs a target <see cref="Attribute"/> with a modifier <see cref="Resource"/> for declarative,
/// status-driven stat modification.
/// Designed to be authored on a <c>StatusRunner</c> via the <c>ActiveStatModifiers</c> export so
/// the modifier is applied while the status is active and reverted on Stop.
/// </summary>
[GlobalClass]
public partial class StatModifier : Resource
{
    /// <summary>Attribute on the target's StatController to modify (e.g. defense, max_speed).</summary>
    [Export, RequiredExport] public Attribute Attribute { get; private set; } = null!;

    /// <summary>
    /// Modifier Resource (typically a typed AttributeModifier, e.g. FloatAttributeModifier) the
    /// target's StatController will dispatch to the matching ModifiableProperty.
    /// </summary>
    [Export, RequiredExport] public Resource Modifier { get; private set; } = null!;

    #region Test Helpers
#if TOOLS
    internal void SetAttribute(Attribute attribute) => Attribute = attribute;
    internal void SetModifier(Resource modifier) => Modifier = modifier;
#endif
    #endregion
}
