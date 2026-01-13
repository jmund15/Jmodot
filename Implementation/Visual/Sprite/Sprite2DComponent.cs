namespace Jmodot.Implementation.Visual.Sprite;

using Core.Visual.Sprite;
using Godot;

/// <summary>
/// A concrete implementation of ISpriteComponent using Godot's built-in Sprite2D node.
/// </summary>
[GlobalClass]
public partial class Sprite2DComponent : Sprite2D, ISpriteComponent
{
    public float GetSpriteHeight() => Texture?.GetHeight() * Scale.Y ?? 0f; // CHECK: check height and width
    public float GetSpriteHalfHeight() => GetSpriteHeight() / 2f;
    public float GetSpriteWidth() => Texture?.GetWidth() * Scale.X ?? 0f;
    public Node GetUnderlyingNode() => this;
}
