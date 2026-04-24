namespace Jmodot.Implementation.Visual.Effects;

using System;
using Godot;

/// <summary>
/// Consumer-facing facade for a <c>VisualComposer</c>'s effect surface.
/// </summary>
/// <remarks>
/// <para>
/// Separates two kinds of visual state that previously required consumers to
/// touch two different subsystems:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Base tint (intent)</b> — "what color is this sprite supposed to be?"
/// Persists across effect frames. Applied via <see cref="SetBaseTint"/> with an
/// <see cref="EffectScope"/> that selects which nodes get the color.
/// </item>
/// <item>
/// <b>Transient effects</b> — flashes, tints, glows applied on top of the base.
/// Triggered through the existing <c>VisualEffectController.PlayEffect</c> surface
/// (scoped effect variants land in a later phase when a driving consumer exists).
/// </item>
/// </list>
/// <para>
/// Pre-Phase 4.5, Wizard.ApplyPlayerColor had to:
/// <list type="number">
/// <item>Compute which nodes to skip (potion slot).</item>
/// <item>Manually loop over composer nodes, registering base colors + writing
/// Modulate.</item>
/// <item>Handle FreeHand visuals separately (not in the composer).</item>
/// <item>Call <c>VisualEffectController.RefreshVisualNodes()</c> to rebuild the
/// controller's base-color cache.</item>
/// </list>
/// Post-4.5, that collapses to three <see cref="SetBaseTint"/> calls — the scope
/// parameter names the "which nodes" question, the service owns the Modulate writes
/// and the cache refresh.
/// </para>
/// </remarks>
public interface IVisualEffectService
{
    /// <summary>
    /// Set the intended base color for every node in the scope. Persists until
    /// overwritten by another <see cref="SetBaseTint"/> or <see cref="ClearBaseTint"/>
    /// touching the same nodes.
    /// </summary>
    void SetBaseTint(Color color, EffectScope scope);

    /// <summary>
    /// Clear any base-tint registration for the scoped nodes, returning them to
    /// untinted white.
    /// </summary>
    void ClearBaseTint(EffectScope scope);

    /// <summary>
    /// Fires after any successful <see cref="SetBaseTint"/> / <see cref="ClearBaseTint"/>.
    /// <c>VisualEffectController</c> subscribes to this to rebuild its base-color cache
    /// without the service needing a direct controller reference.
    /// </summary>
    event Action TintChanged;
}
