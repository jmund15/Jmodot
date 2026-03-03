namespace Jmodot.Core.AI;

using System;
using System.Collections.Generic;

/// <summary>
///     Extended contract for AI subsystems that contain nested <see cref="IDebugPanelProvider"/>
///     children. The dashboard creates expandable sections for each nested provider under
///     this provider's tab.
///
///     <para>Example: CompoundState (HSM) contains BTState children, each with a BehaviorTree
///     that implements <see cref="IDebugPanelProvider"/>. The HSM tab shows its own state history
///     at the top, with expandable BT sections below.</para>
/// </summary>
public interface IDebugNestingProvider : IDebugPanelProvider
{
    /// <summary>
    ///     Nested providers grouped under this provider's tab (e.g., BTs under HSM).
    ///     The dashboard creates expandable sections for each.
    /// </summary>
    IReadOnlyList<IDebugPanelProvider> NestedProviders { get; }

    /// <summary>
    ///     The currently "active" nested provider (e.g., the active BTState's BT).
    ///     Null if no nested provider is active.
    /// </summary>
    IDebugPanelProvider? ActiveNestedProvider { get; }

    /// <summary>
    ///     Fired when the active nested provider changes (e.g., HSM state transition).
    ///     The dashboard uses this to auto-expand the new active provider's section.
    /// </summary>
    event Action<IDebugPanelProvider?> ActiveNestedProviderChanged;
}
