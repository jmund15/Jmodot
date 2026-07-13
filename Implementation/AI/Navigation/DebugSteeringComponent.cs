namespace Jmodot.Implementation.AI.Navigation;

using Godot;
using Jmodot.AI.Navigation;
using Jmodot.Implementation.Shared;

/// <summary>
/// Scene-composed debug overlay for the steering context map. Attach as a child of an NPC root
/// beside its <see cref="AISteeringProcessor3D"/>; when <c>_enableDebug</c> is on it draws, per bin,
/// a stack of arrows colored per contributing consideration (interest positive / danger negative),
/// a hollow marker on hard-masked bins, and a ring on the committed bin. Reads the processor's
/// <see cref="DebugSteeringRecorder"/> — it never mutates steering state.
///
/// Mirrors the flat-sibling Debug&lt;Subsystem&gt;Component family (e.g. DebugFormationComponent).
/// Default OFF: it ships on npc_template (inherited by every NPC) and stays inert until a developer
/// flips the toggle on a specific instance.
/// </summary>
[GlobalClass]
public partial class DebugSteeringComponent : Node
{
    [ExportGroup("Visualization")]

    /// <summary>Master toggle. Off by default so inherited NPC scenes are inert until opted in.</summary>
    [Export] private bool _enableDebug = false;

    /// <summary>World-space length multiplier for the per-bin arrow stacks.</summary>
    [Export(PropertyHint.Range, "0.5, 8.0, 0.5")] private float _arrowScale = 4.0f;

    /// <summary>Vertical lift so arrows clear the ground.</summary>
    [Export(PropertyHint.Range, "0.0, 2.0, 0.1")] private float _heightOffset = 1.0f;

    /// <summary>Marker color for hard-masked bins.</summary>
    [Export] private Color _maskColor = new(1f, 0.15f, 0.15f, 0.6f);

    /// <summary>Ring color for the synthesis-committed bin.</summary>
    [Export] private Color _committedColor = new(1f, 1f, 1f, 0.9f);

    [ExportGroup("References")]

    /// <summary>The steering processor to visualize. If unset, resolved from a sibling at _Ready.</summary>
    [Export] private AISteeringProcessor3D? _processor;

    private DebugSteeringRecorder? _recorder;
    private Node3D? _agent;

    public override void _Ready()
    {
        ResolveAndEnable();
        // No recorder → nothing to draw; skip the per-frame callback entirely on inert instances.
        if (_recorder == null) { SetProcess(false); }
    }

    private void ResolveAndEnable()
    {
        if (!_enableDebug) { return; }

        _processor ??= GetParent()?.GetFirstChildOfType<AISteeringProcessor3D>();
        if (_processor == null)
        {
            JmoLogger.Warning(this, "[Steering] DebugSteeringComponent found no AISteeringProcessor3D sibling; overlay inert.");
            return;
        }

        _recorder = _processor.EnableAttributionRecording();
        _agent = _processor.GetParentOrNull<Node3D>();
    }

    public override void _Process(double delta)
    {
        if (!_enableDebug || _recorder == null || _agent == null || _processor == null) { return; }
        DrawOverlay();
    }

    private void DrawOverlay()
    {
        var bins = _processor!.MovementDirections.OrderedDirections;
        var contributions = _recorder!.Contributions;
        Vector3 origin = _agent!.GlobalPosition + Vector3.Up * _heightOffset;

        for (int i = 0; i < bins.Count; i++)
        {
            Vector3 dir = bins[i];
            float cursor = 0f;
            bool masked = false;

            for (int c = 0; c < contributions.Count; c++)
            {
                var contrib = contributions[c];
                if (contrib.MaskAdded[i]) { masked = true; }

                float net = contrib.InterestDelta[i] - contrib.DangerDelta[i];
                if (Mathf.Abs(net) < 0.001f) { continue; }

                Vector3 from = origin + dir * (cursor * _arrowScale);
                Vector3 to = origin + dir * ((cursor + net) * _arrowScale);
                DebugDraw3D.DrawArrow(from, to, PaletteColor(c), 0.1f, true);
                cursor += net;
            }

            if (masked)
            {
                DebugDraw3D.DrawSphere(origin + dir * _arrowScale, 0.15f, _maskColor);
            }
        }

        int committed = _recorder.CommittedBin;
        if (committed >= 0 && committed < bins.Count)
        {
            DebugDraw3D.DrawSphere(origin + bins[committed] * _arrowScale, 0.25f, _committedColor);
        }
    }

    // Golden-ratio hue spacing gives visually distinct colors for any consideration count.
    private static Color PaletteColor(int index) =>
        Color.FromHsv((index * 0.381966f) % 1f, 0.75f, 0.95f, 0.75f);

    #region Test Helpers
#if TOOLS
    internal bool _TestEnabled => _enableDebug;
    internal void SetEnabledForTest(bool enabled) => _enableDebug = enabled;
    internal void SetProcessorForTest(AISteeringProcessor3D processor) => _processor = processor;
    internal void _TestReady() => ResolveAndEnable();
#endif
    #endregion
}
