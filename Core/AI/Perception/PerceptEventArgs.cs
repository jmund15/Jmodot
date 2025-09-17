namespace Jmodot.Core.AI.Perception;

using System;

/// <summary>
///     Represents the arguments for a perception event, containing the generated Percept.
/// </summary>
public class PerceptEventArgs : EventArgs
{
    public PerceptEventArgs(Percept percept, IAISensor sensor)
    {
        this.Percept = percept;
        this.Sensor = sensor;
    }

    public Percept Percept { get; }
    public IAISensor Sensor { get; }
}
