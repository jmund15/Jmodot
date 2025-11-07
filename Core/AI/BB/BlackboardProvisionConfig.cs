namespace Jmodot.Core.AI.BB;

// BlackboardProvisionConfig.cs

/// <summary>
/// A data container for configuring how a component provides itself to the blackboard.
/// Use this as an [Export] property in components that implement IBlackboardProvider.
/// </summary>
[GlobalClass]
public partial class BlackboardProvisionConfig : Resource
{
    [Export]
    private bool _enabled = true;

    [Export]
    private StringName _key;

    /// <summary>
    /// If true, the component will attempt to register itself.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// The key to use for registration in the blackboard.
    /// </summary>
    public StringName Key => _key;
}
