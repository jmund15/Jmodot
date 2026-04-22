namespace Jmodot.Core.Input;

using Godot;
using GCol = Godot.Collections;

/// <summary>
/// Resource that maps concrete Godot input events to on-screen prompt glyph
/// textures (C3). Three parallel dictionaries — one per InputEvent subclass
/// the project uses — are populated in Inspector from a glyph asset pack
/// (Xelu's Controller Prompts for v1; user's custom art later).
///
/// <para><b>Modular swap story:</b> the entire glyph pack is a single
/// <c>.tres</c> instance of this class. To replace art, author a new
/// <c>custom_glyph_registry.tres</c> with different Texture2D references and
/// swap the Inspector reference on the consumers (typically the
/// <c>GlobalRegistry</c> autoload's <c>GlyphRegistry</c> export). Zero code
/// changes. Future abstraction to a strategy hierarchy only if a non-dict
/// lookup strategy emerges (sprite atlas, icon font, etc.).</para>
///
/// <para><b>Lookup contract:</b> <see cref="GetTexture"/> unwraps the input
/// event's concrete type, reads the matching dictionary, and returns the
/// texture or <c>null</c> if unmapped. Callers fall back to text-only
/// rendering when <c>null</c> is returned.</para>
/// </summary>
[GlobalClass]
public partial class InputGlyphRegistry : Resource
{
    /// <summary>Maps physical keycodes (Key.W, Key.Space, etc.) to glyph textures.</summary>
    [Export] public GCol.Dictionary<Key, Texture2D> KeyboardGlyphs { get; set; } = new();

    /// <summary>Maps gamepad buttons (JoyButton.A, JoyButton.Y, etc.) to glyph textures.</summary>
    [Export] public GCol.Dictionary<JoyButton, Texture2D> GamepadGlyphs { get; set; } = new();

    /// <summary>Maps mouse buttons (MouseButton.Left, etc.) to glyph textures.</summary>
    [Export] public GCol.Dictionary<MouseButton, Texture2D> MouseGlyphs { get; set; } = new();

    /// <summary>
    /// Resolves a Godot InputEvent to its glyph texture, or <c>null</c> if
    /// the event's concrete type isn't handled or its key/button isn't mapped
    /// in the registry. Supports <see cref="InputEventKey"/>,
    /// <see cref="InputEventJoypadButton"/>, and <see cref="InputEventMouseButton"/>.
    /// Other event types (motion, gesture) return null by design.
    /// </summary>
    public Texture2D? GetTexture(InputEvent ev)
    {
        switch (ev)
        {
            case InputEventKey key:
                if (KeyboardGlyphs.TryGetValue(key.PhysicalKeycode, out var keyTex)) { return keyTex; }
                return null;
            case InputEventJoypadButton jb:
                if (GamepadGlyphs.TryGetValue((JoyButton)jb.ButtonIndex, out var jbTex)) { return jbTex; }
                return null;
            case InputEventMouseButton mb:
                if (MouseGlyphs.TryGetValue(mb.ButtonIndex, out var mbTex)) { return mbTex; }
                return null;
            default:
                return null;
        }
    }
}
