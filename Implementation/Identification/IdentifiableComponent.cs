namespace Jmodot.Implementation.Identification;

using Core.Identification;
using Jmodot.Core.Shared.Attributes;

/// <summary>
///     A generic component for giving simple scene objects (props, triggers, etc.) a semantic identity.
///     For complex actors like the Player or Monsters, the IIdentifiable interface should be implemented
///     directly on their main C# script instead of using this component.
/// </summary>
[GlobalClass]
public partial class IdentifiableComponent : Node, IIdentifiable
{
    /// <summary>The Identity resource that defines this simple object.</summary>
    [Export, RequiredExport]
    public Identity Identity { get; private set; } = null!;

    public override void _Ready()
    {
        base._Ready();
        if (Engine.IsEditorHint()) { return; }
        this.ValidateRequiredExports();
    }

    /// <inheritdoc />
    public Identity GetIdentity()
    {
        return this.Identity;
    }
}
