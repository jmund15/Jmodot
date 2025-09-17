#region

using System;
using Jmodot.Core.Shared;

#endregion

namespace Jmodot.Core.AI.Perception;

/// <summary>
///     The core interface for any sensor in the AI system. A sensor's only job is to
///     observe the world and fire an event when it perceives something. It is responsible
///     for generating a `Percept` struct containing all relevant contextual information about the sensation.
/// </summary>
public interface IAISensor : IGodotNodeInterface
{
    /// <summary>
    ///     Fired when a new percept is created or an existing one is updated with new information.
    ///     The AIPerceptionManager subscribes to this event to populate its memory bank.
    /// </summary>
    event EventHandler<PerceptEventArgs> PerceptUpdated;
}