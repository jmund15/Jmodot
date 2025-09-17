namespace Jmodot.Implementation.Identification;

using Core.Identification;

/// <summary>
///     A generic component for giving simple scene objects (props, triggers, etc.) a semantic identity.
///     For complex actors like the Player or Monsters, the IIdentifiable interface should be implemented
///     directly on their main C# script instead of using this component.
/// </summary>
[GlobalClass]
public partial class IdentifiableComponent : Node, IIdentifiable
{
    /// <summary>The Identity resource that defines this simple object.</summary>
    [Export]
    public Identity Identity { get; private set; }

    /// <inheritdoc />
    public Identity GetIdentity()
    {
        return this.Identity;
    }
}
