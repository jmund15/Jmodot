namespace Jmodot.Core.Visual.Sprite;

using Godot;
using Shared;

/// <summary>
/// Defines a contract for components that have sprite-like visual properties.
/// This allows game logic to manipulate visual aspects like flipping or offset
/// without being tied to a specific Sprite3D or Sprite2D node.
/// </summary>
public interface ISpriteComponent : IGodotNodeInterface
{
    float GetSpriteHeight();
    float GetSpriteHalfHeight();
    float GetSpriteWidth();
    bool FlipH { get; set; }
    bool FlipV { get; set; }
    Vector2 Offset { get; set; }
    Texture2D? GetTexture();
    void Hide();
    void Show();
}
