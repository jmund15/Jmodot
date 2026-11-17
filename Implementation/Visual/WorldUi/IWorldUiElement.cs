namespace Jmodot.Implementation.Visual.WorldUi;

using System;

/// <summary>
/// One piece of world-space UI that a <see cref="WorldUiPrompt"/> can stack above or below the art
/// it annotates. The prompt owns only WHERE an element sits; the element owns everything about how
/// it renders.
/// </summary>
/// <remarks>
/// Obligations of every implementation:
/// <list type="bullet">
/// <item>It is a <c>Node3D</c> that is a direct child of the prompt listing it, because the prompt
/// writes its <c>Position</c>.</item>
/// <item>Every node it draws billboards, draws with <c>no_depth_test</c>, and draws at render
/// priority <see cref="WorldUiLabel.WorldUiRenderPriority"/>. The element sets all three on every
/// node it owns; no scene or caller sets them, so each invariant has one home.</item>
/// <item>Its drawn box is ONE line of items, centred on the node origin. The prompt writes the origin
/// as the box's centre and clears one <see cref="RenderedHeight"/> around it, so anything that moves
/// the box off the origin or adds a line overlaps the art or the next element.</item>
/// <item><see cref="RenderedHeight"/> is the height the element DRAWS, in metres. Because the element
/// billboards, that height is also its screen height, which is what the prompt clears.</item>
/// <item><see cref="LayoutChanged"/> is raised whenever <see cref="RenderedHeight"/> or the drawn
/// content can change. A caller that changes an element never has to ask the prompt to refresh.</item>
/// </list>
/// Editor-time type tests against this interface are only valid on <c>[Tool]</c> implementations.
/// </remarks>
public interface IWorldUiElement
{
    /// <summary>Drawn height in metres; also the screen height, since every element billboards.</summary>
    float RenderedHeight { get; }

    /// <summary>Raised when the drawn size or content can have changed.</summary>
    event Action LayoutChanged;
}
