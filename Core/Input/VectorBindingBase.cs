namespace Jmodot.Core.Input;

using Jmodot.Core.Shared.Attributes;

[GlobalClass, Tool]
public abstract partial class VectorBindingBase : Resource
{
    [Export, RequiredExport] public InputAction Action { get; set; } = null!;
    public abstract Vector2 GetVectorInput(Node3D entity);

    /// <summary>
    /// Declares what the emitted Vector2 semantically represents so consumers
    /// can interpret it correctly without brittle magnitude heuristics.
    /// </summary>
    public abstract VectorInputSemantic Semantic { get; }

    /// <summary>
    /// Hint for on-screen prompt rendering (C3). A vector input doesn't map
    /// to a single button, so prompt systems use this enum to select a
    /// human-readable cluster label ("WASD" / "Arrows" / "Left Stick") or a
    /// future composite-key visualization. Per-binding authored — the same
    /// binding type can produce different hints across profiles (keyboard_wasd
    /// vs keyboard_arrows vs gamepad).
    /// </summary>
    [Export] public VectorGlyphHint GlyphHint { get; set; } = VectorGlyphHint.WasdCluster;
}
