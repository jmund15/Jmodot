namespace Jmodot.Implementation.Combat;

using AI.BB;
using Core.Combat.Reactions;
using Godot;
using Jmodot.Core.Components;
using Jmodot.Core.AI.BB;
using Jmodot.Implementation.Combat;
using Jmodot.Core.Combat;
using Shared;
using System.Text;
using Status;

/// <summary>
/// Listens to the Combatant and pushes results into the Blackboard's Event Log.
/// </summary>
[GlobalClass]
public partial class CombatLogger : Node, IComponent
{
    [Export] public CombatantComponent Combatant { get; private set; }

    // The key where we store the Log object in the BB
    public const string BB_CombatLog = "CombatLogger";

    /// <summary>
    /// Set to true to enable verbose logging of combat events to the Godot console.
    /// Toggle this during debugging/testing sessions.
    /// </summary>
    public static bool VerboseLoggingEnabled { get; set; } = false;

    private CombatLog _log;

    public bool Initialize(IBlackboard bb)
    {
        // 1. Create or Retrieve the Log
        if (!bb.TryGet(BB_CombatLog, out _log))
        {
            _log = new CombatLog();
            bb.Set(BB_CombatLog, _log);
        }

        // 2. Resolve Dependency
        if (!bb.TryGet<CombatantComponent>(BBDataSig.CombatantComponent, out var c))
        {
            JmoLogger.Error(this, $"CombatLogger must have a Combatant Component to operate!");
            return false;
        }
        Combatant = c!;

        IsInitialized = true;
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize()
    {
        Combatant.CombatResultEvent += HandleCombatResultEvent;
    }

    private void HandleCombatResultEvent(CombatResult result)
    {
        _log?.Log(result);

        if (VerboseLoggingEnabled)
        {
            LogVerbose(result);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Optional: Keep memory usage low
        //_log?.PruneAllButCurrentFrame();
    }

    public override void _ExitTree()
    {
        if (Combatant != null)
        {
            Combatant.CombatResultEvent -= HandleCombatResultEvent;
        }
    }

    #region Verbose Logging

    /// <summary>
    /// Outputs detailed combat event information to the Godot console for debugging.
    /// Call this manually or set VerboseLoggingEnabled = true for automatic logging.
    /// </summary>
    /// <param name="result">The combat result to log.</param>
    public void LogVerbose(CombatResult result)
    {
        if (result == null)
        {
            JmoLogger.Warning(this, "LogVerbose called with null result");
            return;
        }

        var sb = new StringBuilder();
        var frameId = Engine.GetPhysicsFrames();
        var combatTime = _log?.CombatTimeElapsed ?? 0f;

        // Header
        sb.AppendLine("╔══════════════════════════════════════════════════════════════");
        sb.AppendLine($"║ COMBAT EVENT: {result.GetType().Name}");
        sb.AppendLine($"║ Frame: {frameId} | Combat Time: {combatTime:F3}s");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════");

        // Source and Target
        var sourceName = result.Source?.Name ?? "(null)";
        var targetName = result.Target?.Name ?? "(null)";
        sb.AppendLine($"║ Source: {sourceName}");
        sb.AppendLine($"║ Target: {targetName}");

        // Tags
        if (result.Tags != null)
        {
            sb.Append("║ Tags: ");
            var tagNames = new System.Collections.Generic.List<string>();
            foreach (var tag in result.Tags)
            {
                if (tag != null)
                {
                    tagNames.Add($"{tag.TagId} (P:{tag.Priority})");
                }
            }
            sb.AppendLine(tagNames.Count > 0 ? string.Join(", ", tagNames) : "(none)");
        }

        // Type-specific details
        sb.AppendLine("╠──────────────────────────────────────────────────────────────");
        AppendResultDetails(sb, result);

        sb.AppendLine("╚══════════════════════════════════════════════════════════════");

        JmoLogger.Info(this, sb.ToString());
    }

    private void AppendResultDetails(StringBuilder sb, CombatResult result)
    {
        switch (result)
        {
            case DamageResult dmg:
                sb.AppendLine($"║ Original Amount: {dmg.OriginalAmount:F2}");
                sb.AppendLine($"║ Final Amount:    {dmg.FinalAmount:F2} (after armor/mitigation)");
                sb.AppendLine($"║ Is Critical:     {dmg.IsCritical}");
                sb.AppendLine($"║ Is Fatal:        {dmg.IsFatal}");
                break;

            case HealResult heal:
                sb.AppendLine($"║ Amount Healed:   {heal.AmountHealed:F2}");
                sb.AppendLine($"║ Overhealing:     {heal.Overhealing:F2}");
                break;

            case StatusResult status:
                var runnerType = status.Runner?.GetType().Name ?? "(null)";
                var runnerTags = status.Runner?.Tags;
                sb.AppendLine($"║ Status Runner:   {runnerType}");
                if (runnerTags != null)
                {
                    sb.Append("║ Runner Tags:     ");
                    var runnerTagNames = new System.Collections.Generic.List<string>();
                    foreach (var tag in runnerTags)
                    {
                        if (tag != null) runnerTagNames.Add(tag.TagId);
                    }
                    sb.AppendLine(string.Join(", ", runnerTagNames));
                }
                break;

            case StatusExpiredResult expired:
                sb.AppendLine($"║ Was Dispelled:   {expired.WasDispelled}");
                sb.AppendLine($"║ Ended:           {(expired.WasDispelled ? "Manually stopped" : "Completed naturally")}");
                break;

            case StatResult stat:
                sb.AppendLine("║ Stat modification applied (details TODO)");
                break;

            case EffectResult effect:
                sb.AppendLine($"║ Effect:          {effect.EffectDescription ?? "(no description)"}");
                break;

            default:
                sb.AppendLine($"║ (Unknown result type: {result.GetType().Name})");
                break;
        }
    }

    #endregion

    // ... IComponent ...
    public bool IsInitialized { get; private set; }
    public event System.Action Initialized;
    public Node GetUnderlyingNode() => this;
}
