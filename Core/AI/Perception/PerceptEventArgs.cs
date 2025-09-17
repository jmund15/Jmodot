#region

using System;

#endregion

namespace Jmodot.Core.AI.Perception;

/// <summary>
///     Represents the arguments for a perception event, containing the generated Percept.
/// </summary>
public class PerceptEventArgs : EventArgs
{
    public PerceptEventArgs(Percept percept, IAISensor sensor)
    {
        Percept = percept;
        Sensor = sensor;
    }

    public Percept Percept { get; }
    public IAISensor Sensor { get; }
}