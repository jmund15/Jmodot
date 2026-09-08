namespace Jmodot.Core.Stats;

using Godot;
using Core.Modifiers;
using Shared.Attributes;

/// <summary>
/// Pairs a target <see cref="Attribute"/> with an <see cref="AttributeModifier"/> for declarative stat modification.
/// </summary>
[GlobalClass, Tool]
public partial class StatModifier : Resource
{
    /// <summary>Attribute on the target's StatController to modify (e.g. defense, max_speed).</summary>
    [Export, RequiredExport] public Attribute Attribute { get; private set; } = null!;

    /// <summary>Attribute modifier dispatched to the matching ModifiableProperty.</summary>
    [Export, RequiredExport] public AttributeModifier Modifier { get; private set; } = null!;

    public StatModifier() { }

    public StatModifier(Attribute attribute, AttributeModifier modifier)
    {
        Attribute = attribute;
        Modifier = modifier;
    }

    #region Test Helpers
#if TOOLS
    internal void SetAttribute(Attribute attribute) => Attribute = attribute;
    internal void SetModifier(AttributeModifier modifier) => Modifier = modifier;
#endif
    #endregion
}
