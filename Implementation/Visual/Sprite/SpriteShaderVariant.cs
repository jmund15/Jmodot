namespace Jmodot.Implementation.Visual.Sprite;

using System;
using System.Collections.Generic;
using Godot;

/// <summary>Render state selected from a sprite's draw flags for its shader override.</summary>
public readonly record struct SpriteShaderVariant(bool Shaded, BaseMaterial3D.TransparencyEnum Transparency, bool DoubleSided,
    BaseMaterial3D.BillboardModeEnum Billboard, bool NoDepthTest, bool FixedSize,
    BaseMaterial3D.TextureFilterEnum Filter, int RenderPriority)
{
    public static SpriteShaderVariant Of(SpriteBase3D sprite)
    {
        var transparency = !sprite.Transparent ? BaseMaterial3D.TransparencyEnum.Disabled : sprite.AlphaCut switch
        {
            SpriteBase3D.AlphaCutMode.Discard => BaseMaterial3D.TransparencyEnum.AlphaScissor,
            SpriteBase3D.AlphaCutMode.OpaquePrepass => BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass,
            SpriteBase3D.AlphaCutMode.Hash => BaseMaterial3D.TransparencyEnum.AlphaHash,
            _ => BaseMaterial3D.TransparencyEnum.Alpha,
        };
        // The engine applies a sprite material's render priority only while its alpha-cut mode is disabled
        // (sprite_3d.cpp:313), which the transparent flag does not decide: an opaque sprite with a cut mode clears it too.
        return new SpriteShaderVariant(sprite.Shaded, transparency, sprite.DoubleSided, sprite.Billboard,
            sprite.NoDepthTest, sprite.FixedSize, sprite.TextureFilter,
            sprite.AlphaCut == SpriteBase3D.AlphaCutMode.Disabled ? sprite.RenderPriority : 0);
    }

    /// <summary>Emits a spatial shader with literal render modes and the selected sprite macros.</summary>
    public string Code(string bodyPath)
    {
        var modes = new List<string>();
        if (!Shaded) { modes.Add("unshaded"); }
        modes.Add(DoubleSided ? "cull_disabled" : "cull_back");
        if (NoDepthTest) { modes.Add("depth_test_disabled"); }
        if (Transparency == BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass) { modes.Add("depth_prepass_alpha"); }
        if (Transparency is BaseMaterial3D.TransparencyEnum.Alpha or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass)
        {
            modes.Add("blend_premul_alpha");
        }
        var defines = new List<string>();
        if (!Shaded) { defines.Add("#define SPRITE_UNSHADED"); }
        if (Billboard == BaseMaterial3D.BillboardModeEnum.Enabled) { defines.Add("#define SPRITE_BILLBOARD"); }
        if (Billboard == BaseMaterial3D.BillboardModeEnum.FixedY) { defines.Add("#define SPRITE_BILLBOARD_FIXED_Y"); }
        if (FixedSize) { defines.Add("#define SPRITE_FIXED_SIZE"); }
        switch (Transparency)
        {
            case BaseMaterial3D.TransparencyEnum.Alpha:
            case BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass:
                defines.Add("#define SPRITE_ALPHA_BLEND");
                break;
            case BaseMaterial3D.TransparencyEnum.AlphaScissor:
                defines.Add("#define SPRITE_ALPHA_SCISSOR");
                break;
            case BaseMaterial3D.TransparencyEnum.AlphaHash:
                defines.Add("#define SPRITE_ALPHA_HASH");
                break;
        }
        var filter = Filter switch
        {
            BaseMaterial3D.TextureFilterEnum.Nearest => "filter_nearest",
            BaseMaterial3D.TextureFilterEnum.Linear => "filter_linear",
            BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps => "filter_nearest_mipmap",
            BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps => "filter_linear_mipmap",
            BaseMaterial3D.TextureFilterEnum.NearestWithMipmapsAnisotropic => "filter_nearest_mipmap_anisotropic",
            BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic => "filter_linear_mipmap_anisotropic",
            _ => throw new ArgumentOutOfRangeException(nameof(Filter)),
        };
        defines.Add($"#define SPRITE_TEXTURE_FILTER {filter}");
        return $"shader_type spatial;\nrender_mode {string.Join(", ", modes)};\n{string.Join("\n", defines)}\n#include \"{bodyPath}\"\n";
    }
}
