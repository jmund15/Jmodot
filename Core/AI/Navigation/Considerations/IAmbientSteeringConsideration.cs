namespace Jmodot.Core.AI.Navigation.Considerations;

/// <summary>
/// Marks a consideration that stays live under a <c>NavigationOnly</c> control claim (spacing from
/// allies is the case). The processor skips every other consideration while a behavior owns
/// navigation; an ambient term keeps scoring beside the nav path.
/// </summary>
public interface IAmbientSteeringConsideration
{
}
