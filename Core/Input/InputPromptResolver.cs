namespace Jmodot.Core.Input;

using System;
using System.Collections.Generic;
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
    /// Like <see cref="Resolve"/>, but returns every glyph texture that
    /// resolves for the action — enabling multi-icon displays for actions
    /// with multiple events bound (e.g., WASD craft_select = Space + LMB).
    /// Events that don't match any texture in the registry are skipped
    /// silently. Icon order mirrors Godot's InputMap insertion order.
    /// </summary>
    /// <returns>
    /// <c>(icons, label)</c> — <c>icons</c> is empty when unbound OR when
    /// no event has a registered glyph (consumer falls back to label-only).
    /// <c>label</c> is <see cref="UnboundFallback"/> when unbound, else
    /// <see cref="InputAction.ActionName"/>.
    /// </returns>
    public static (IReadOnlyList<Texture2D> icons, string label) ResolveAll(
        InputAction action,
        InputMappingProfile profile,
        InputGlyphRegistry registry)
    {
        var binding = profile.ActionBindings.FirstOrDefault(b => b != null && b.Action == action);
        if (binding == null)
        {
            return (Array.Empty<Texture2D>(), UnboundFallback);
        }

        var events = InputMap.ActionGetEvents(binding.GodotActionName);
        if (events.Count == 0)
        {
            return (Array.Empty<Texture2D>(), UnboundFallback);
        }

        var icons = new List<Texture2D>(events.Count);
        foreach (var ev in events)
        {
            var tex = registry.GetTexture(ev);
            if (tex != null) { icons.Add(tex); }
        }

        return (icons, action.ActionName);
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
