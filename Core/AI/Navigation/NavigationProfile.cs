namespace Jmodot.Core.AI.Navigation;

/// <summary>
///     This resource defines the navigation profile for an AI agent, specifying which navigation layers it can traverse.
/// </summary>
[GlobalClass]
public partial class NavigationProfile : Resource
{
    [Export(PropertyHint.Layers3DNavigation)]
    public uint NavigationLayers { get; private set; } = 1;
}
