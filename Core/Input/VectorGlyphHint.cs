namespace Jmodot.Core.Input;

/// <summary>
/// Discriminator for how a <see cref="VectorBindingBase"/> should render as an
/// on-screen input prompt glyph (C3). Vector bindings represent 2D inputs that
/// don't map to a single button — a directional stick, a WASD cluster, an
/// arrow-key cluster — so prompt systems need a hint for which text label or
/// composite visualization to use.
///
/// <para>v1 uses text-only labels ("WASD", "Arrows", "Left Stick"). A future
/// composite-key renderer (WASD-as-four-keys) can extend the resolver without
/// changing this enum.</para>
/// </summary>
public enum VectorGlyphHint
{
    /// <summary>Keyboard WASD cluster. Renders as "WASD".</summary>
    WasdCluster,

    /// <summary>Keyboard arrow-key cluster. Renders as "Arrows".</summary>
    ArrowCluster,

    /// <summary>Gamepad left analog stick. Renders as "Left Stick".</summary>
    LeftStick,

    /// <summary>Gamepad right analog stick. Renders as "Right Stick".</summary>
    RightStick,

    /// <summary>Gamepad D-Pad. Renders as "D-Pad".</summary>
    DpadCluster,

    /// <summary>Mouse cursor position. Renders as "Mouse".</summary>
    MousePosition,
}
