namespace Jmodot.Core.Input;

using System.Linq;
using Godot;

/// <summary>
/// Pure static resolver: maps (<see cref="InputAction"/>,
/// <see cref="InputMappingProfile"/>, <see cref="InputGlyphRegistry"/>) to
/// (Texture2D icon, string label) for on-screen input prompts (C3).
///
/// <para>Resolution path: finds the <see cref="ActionBinding"/> for the
/// action in the profile, queries Godot's InputMap for the bound events
/// (<c>InputMap.ActionGetEvents</c>), takes the <b>first</b> event (Godot
/// preserves insertion order; first is the canonical event for the profile),
/// and looks up the glyph texture in the registry. Label is derived from
/// <see cref="InputAction.ActionName"/>.</para>
///
/// <para><b>Unbound contract:</b> when the action isn't in the profile OR
/// the profile's binding has no events in Godot's InputMap, the resolver
/// returns <c>(null, "—")</c>. The dash signals "action exists but isn't
/// bound" — consumers render it instead of hiding the prompt silently so
/// players get feedback when bindings are misconfigured.</para>
///
/// <para><b>Registry miss contract:</b> when a binding + event exist but the
/// registry has no texture for that keycode/button, the resolver returns
/// <c>(null, ActionName)</c>. Label is meaningful; display falls back to
/// text-only rendering.</para>
///
/// <para><b>Rebinding-compatible by design:</b> resolution happens at call
/// time (not profile-load time), so a future rebinding UI that mutates the
/// Godot InputMap at runtime is automatically reflected the next time a
/// prompt display refreshes.</para>
/// </summary>
public static class InputPromptResolver
{
    /// <summary>Fallback label returned when the action is unbound in the profile.</summary>
    public const string UnboundFallback = "—";

    /// <summary>
    /// Resolves an action to its renderable (icon, label) pair for the given
    /// profile and glyph registry. See class docblock for the full contract.
    /// </summary>
    public static (Texture2D? icon, string label) Resolve(
        InputAction action,
        InputMappingProfile profile,
        InputGlyphRegistry registry)
    {
        var binding = profile.ActionBindings.FirstOrDefault(b => b != null && b.Action == action);
        if (binding == null)
        {
            return (null, UnboundFallback);
        }

        var events = InputMap.ActionGetEvents(binding.GodotActionName);
        if (events.Count == 0)
        {
            return (null, UnboundFallback);
        }

        var icon = registry.GetTexture(events[0]);
        return (icon, action.ActionName);
    }

    /// <summary>
    /// Resolves a vector binding's <see cref="VectorGlyphHint"/> to its
    /// human-readable cluster label for v1 (text-only rendering). Future
    /// versions may add a composite-key renderer (WASD-as-four-keys) — this
    /// method stays as the text fallback.
    /// </summary>
    public static string ResolveVectorLabel(VectorGlyphHint hint) => hint switch
    {
        VectorGlyphHint.WasdCluster => "WASD",
        VectorGlyphHint.ArrowCluster => "Arrows",
        VectorGlyphHint.LeftStick => "Left Stick",
        VectorGlyphHint.RightStick => "Right Stick",
        VectorGlyphHint.DpadCluster => "D-Pad",
        VectorGlyphHint.MousePosition => "Mouse",
        _ => "Move",
    };
}
