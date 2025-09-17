namespace Jmodot.Implementation.Visual.Sprite;

using Core.Visual.Sprite;
using Godot;

/// <summary>
/// A concrete implementation of ISpriteComponent using Godot's built-in Sprite3D node.
/// </summary>
[GlobalClass]
public partial class Sprite3DComponent : Sprite3D, ISpriteComponent
{
    public float GetSpriteHeight() => Texture?.GetHeight() * PixelSize * Scale.Y ?? 0f;
    public float GetSpriteWidth() => Texture?.GetWidth() * PixelSize * Scale.X ?? 0f;
    public Node GetUnderlyingNode() => this;
}
