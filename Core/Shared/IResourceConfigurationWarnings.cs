namespace Jmodot.Core.Shared;

/// <summary>
/// Author-time configuration warnings for a <see cref="Resource"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>_GetConfigurationWarnings</c> is a <see cref="Node"/> virtual and Godot never calls it on a
/// Resource, so an authored Resource has no rung of its own: a missing or resave-stripped export
/// degrades silently at runtime instead of failing loudly in the editor. A Node that owns such a
/// Resource lends it one by forwarding this method from its own
/// <c>_GetConfigurationWarnings</c>.
/// </para>
/// <para>
/// Warnings are returned SEPARATELY rather than pre-joined, because the Inspector renders one
/// bullet per array element; joining faults hides every fault after the first.
/// </para>
/// <para>
/// This exists as an interface so a host forwards for ANY Resource that opts in. A host that type
/// tests for one concrete Resource instead silently skips every sibling that later needs
/// validation, which is how four implementers of this method accumulated with no caller at all.
/// </para>
/// </remarks>
public interface IResourceConfigurationWarnings
{
    /// <summary>
    /// One entry per distinct authoring fault, each naming the Inspector label its author would
    /// look for and what the Resource does instead. Empty when the Resource is fully authored.
    /// </summary>
    string[] GetResourceConfigurationWarnings();
}
