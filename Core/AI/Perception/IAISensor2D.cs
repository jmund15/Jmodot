namespace Jmodot.Core.AI.Perception;

using System;
using Shared;

/// <summary>
///     The core interface for any sensor in the AI system. A sensor's only job is to
///     observe the world and fire an event when it perceives something. It is responsible
///     for generating a `Percept` struct containing all relevant contextual information about the sensation.
/// </summary>
public interface IAISensor2D : IGodotNodeInterface
{
    /// <summary>
    ///     Fired when a new percept is created or an existing one is updated with new information.
    ///     The AIPerceptionManager subscribes to this event to populate its memory bank.
    /// </summary>
    event Action<IAISensor2D, Percept2D> PerceptUpdated;
}
