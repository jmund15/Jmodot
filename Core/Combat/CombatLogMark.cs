namespace Jmodot.Core.Combat;

/// <summary>A log position including in-tick ordering. Only use with the log that produced it.</summary>
public readonly record struct CombatLogMark(float CombatTime, int CountAtTime)
{
    public static CombatLogMark Origin => default;
}
