namespace Jmodot.Implementation.Shared;

using AI.Navigation;

public class FutureImpls
{
    /// <summary>
    /// Checks if the agent has stopped moving while it still has a path to follow.
    /// This is a crucial self-correction mechanism.
    /// </summary>
    private void MonitorForStuckCondition(double delta)
    {
        // TEMP VARS
        float StuckTimeThreshold = 0;
        CharacterBody3D _body = null!;
        AINavigator3D _navigator = null!;
        double _stuckTimer = 0;

        if (_navigator.IsNavigationFinished())
        {
            _stuckTimer = 0.0;
            return;
        }

        if (_body.Velocity.LengthSquared() < 0.01f) // Use a small threshold
        {
            _stuckTimer += delta;
            if (_stuckTimer > StuckTimeThreshold)
            {
                JmoLogger.Info(this, "Agent appears to be stuck. Attempting to repath.");
                // Tell the navigator to try again with the same strategic target position.
                _navigator.RequestNewNavPath(_navigator.TargetPosition);
                _stuckTimer = 0.0; // Reset timer after attempting to fix.
            }
        }
        else
        {
            _stuckTimer = 0.0;
        }
    }
}
