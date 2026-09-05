namespace Jmodot.Core.Stats;

using Implementation.Shared.GodotExceptions;

/// <summary>
///     A data-driven "tag" Resource that represents a specific character statistic
///     (e.g., "MaxSpeed", "JumpHeight", "AirControl"). It is used as a type-safe key
///     in dictionaries, replacing brittle enums or strings and allowing designers
///     to define new stats without changing code.
/// </summary>
[GlobalClass, Tool]
public partial class Attribute : Resource
{
    [Export] public string AttributeName { get; private set; } = "Unnamed Attribute";

    /// <summary>Display group for authoring tools; free text, blank renders as an "Other" bucket.</summary>
    [ExportGroup("Authoring")]
    [Export] public string Group { get; private set; } = "";

    /// <summary>Designer-facing description; shown as the row tooltip in authoring tools.</summary>
    [Export(PropertyHint.MultilineText)] public string Description { get; private set; } = "";

    /// <summary>Display unit suffix (e.g. "s", "m/s"); blank = dimensionless.</summary>
    [Export] public string Unit { get; private set; } = "";

    /// <summary>Authoring-time bound only: tools clamp editors and lint authored values to [MinValue, MaxValue] by Step. Nothing at runtime clamps a resolved stat to it.</summary>
    [ExportSubgroup("Range")]
    [Export] public bool HasRange { get; private set; }
    [Export] public float MinValue { get; private set; }
    [Export] public float MaxValue { get; private set; }
    [Export] public float Step { get; private set; }

    public static Attribute CreateTestAttribute(string testName) => new() { AttributeName = testName };

    /// <summary>
    ///     Throws <see cref="ResourceConfigurationException"/> when <see cref="HasRange"/> is set with a
    ///     non-finite bound, MinValue >= MaxValue, or Step &lt;= 0; returns silently otherwise.
    /// </summary>
    public void ValidateRange()
    {
        if (!this.HasRange) { return; }

        // Every comparison against NaN is false, so the ordering and positivity tests below both admit it.
        if (!float.IsFinite(this.MinValue) || !float.IsFinite(this.MaxValue) || !float.IsFinite(this.Step))
        {
            throw new ResourceConfigurationException(
                $"Range bounds must be finite (Min {this.MinValue}, Max {this.MaxValue}, Step {this.Step}) for attribute '{this.AttributeName}'.", this);
        }

        if (this.MinValue >= this.MaxValue)
        {
            throw new ResourceConfigurationException(
                $"MinValue {this.MinValue} >= MaxValue {this.MaxValue} for attribute '{this.AttributeName}'.", this);
        }

        if (this.Step <= 0f)
        {
            throw new ResourceConfigurationException(
                $"Step {this.Step} must be positive for ranged attribute '{this.AttributeName}'.", this);
        }
    }

    #region Test Helpers
#if TOOLS
    internal void SetGroup(string group) => this.Group = group;
    internal void SetUnit(string unit) => this.Unit = unit;

    internal void SetRange(bool hasRange, float min, float max, float step)
    {
        this.HasRange = hasRange;
        this.MinValue = min;
        this.MaxValue = max;
        this.Step = step;
    }
#endif
    #endregion
}
