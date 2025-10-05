namespace Jmodot.Core.Stats;

[GlobalClass]
public partial class StatContext : Resource
{
    [Export]
    public string ContextName { get; private set; } = "Unnamed Context";
}
