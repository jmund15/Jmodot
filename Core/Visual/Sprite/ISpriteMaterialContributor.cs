namespace Jmodot.Core.Visual.Sprite;

using Godot;

/// <summary>A child under a binder contributes to every sprite it binds; a child under one bound sprite
/// replaces the binder's contribution with the same slot on that sprite, whole. Contribute only uniforms
/// declared by the binder's body, never its texture, frame or alpha uniforms, and never replace the material.</summary>
public interface ISpriteMaterialContributor
{
    /// <summary>Identifies the group of uniforms this contributor owns; constant per type.</summary>
    StringName Slot { get; }
    void ContributeTo(SpriteBase3D sprite, ShaderMaterial material);
}
