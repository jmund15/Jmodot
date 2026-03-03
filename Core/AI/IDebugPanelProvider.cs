namespace Jmodot.Core.AI;

/// <summary>
///     Base contract for any AI subsystem that can provide debug visualization content
///     to the unified <c>AIDebugDashboard</c>. Implementations return a Control subtree
///     for display in a tabbed dashboard panel.
///
///     <para><b>Tab ordering conventions:</b> HSM=0, BTs=10-19, Emotions=20, Affinities=30, Steering=40.</para>
/// </summary>
public interface IDebugPanelProvider
{
    /// <summary>Display name for the tab (e.g., "Behavior", "Wander BT", "Emotions").</summary>
    string DebugTabName { get; }

    /// <summary>
    ///     Priority for tab ordering (lower = further left).
    ///     Conventions: HSM=0, BTs=10-19, Emotions=20, Affinities=30, Steering=40.
    /// </summary>
    int DebugTabOrder { get; }

    /// <summary>
    ///     Creates the Control subtree for this provider's debug content.
    ///     Called lazily on first tab selection. The dashboard owns the returned Control.
    /// </summary>
    Control CreateDebugContent();

    /// <summary>Called per frame when this provider's tab is active and visible.</summary>
    void UpdateDebugContent(double delta);

    /// <summary>Called when this provider's tab becomes inactive (pause expensive updates).</summary>
    void OnDebugContentHidden();

    /// <summary>Whether this provider has meaningful data to display right now.</summary>
    bool HasDebugData { get; }
}
