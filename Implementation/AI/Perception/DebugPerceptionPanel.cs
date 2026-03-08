namespace Jmodot.Implementation.AI.Perception;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.AI.Perception;

/// <summary>
/// Debug visualization panel for AIPerceptionManager3D. Displays sensor status,
/// active memory rows with confidence bars, and a scrollable perception event log.
/// Always embedded inside AIDebugDashboard — never standalone.
/// </summary>
public partial class DebugPerceptionPanel : Control
{
    private const int MaxLogEntries = 50;
    private const float ConfidenceBarMaxWidth = 100f;

    private AIPerceptionManager3D? _manager;
    private VBoxContainer _layout = null!;
    private VBoxContainer _sensorSection = null!;
    private Label _memoryHeader = null!;
    private VBoxContainer _memorySection = null!;
    private ScrollContainer _logScroll = null!;
    private VBoxContainer _logSection = null!;
    private ulong _startTime;

    private readonly Queue<string> _logBuffer = new();
    private readonly List<MemoryRowCache> _memoryRowPool = new();
    private bool _subscribed;

    private class MemoryRowCache
    {
        public HBoxContainer Row { get; set; } = null!;
        public Label NameLabel { get; set; } = null!;
        public ColorRect BarBg { get; set; } = null!;
        public ColorRect BarFill { get; set; } = null!;
        public Label ConfidenceLabel { get; set; } = null!;
    }

    /// <summary>Number of sensor rows currently displayed.</summary>
    public int SensorDisplayCount => _sensorSection?.GetChildCount() ?? 0;

    /// <summary>Number of active memory rows currently displayed.</summary>
    public int MemoryRowCount => _memoryRowPool.Count(r => r.Row.Visible);

    /// <summary>Number of log entries currently displayed.</summary>
    public int LogEntryCount => _logBuffer.Count;

    /// <summary>
    /// Initializes the panel with its owning perception manager.
    /// Builds the static layout and subscribes to perception events.
    /// </summary>
    public void Init(AIPerceptionManager3D manager)
    {
        _manager = manager;
        _startTime = Time.GetTicksMsec();
        BuildLayout();
        PopulateSensors();
        Subscribe();
    }

    /// <summary>
    /// Refreshes the memory row display from current active memories.
    /// Called per-frame when the perception tab is visible.
    /// </summary>
    public void Refresh()
    {
        if (_manager == null) { return; }

        var memories = _manager.GetAllActiveMemories()
            .OrderByDescending(m => m.CurrentConfidence)
            .ToList();

        _memoryHeader.Text = $"Active Memories ({memories.Count})";

        // Reuse pooled rows, create new ones only if needed
        for (int i = 0; i < memories.Count; i++)
        {
            if (i >= _memoryRowPool.Count)
            {
                _memoryRowPool.Add(CreateMemoryRow());
            }

            var cache = _memoryRowPool[i];
            UpdateMemoryRow(cache, memories[i]);
            cache.Row.Visible = true;
        }

        // Hide surplus rows
        for (int i = memories.Count; i < _memoryRowPool.Count; i++)
        {
            _memoryRowPool[i].Row.Visible = false;
        }
    }

    #region Layout

    private void BuildLayout()
    {
        _layout = new VBoxContainer
        {
            Name = "PerceptionLayout",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(_layout);
        _layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Sensor header section
        var sensorHeader = new Label
        {
            Text = "Sensors",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _layout.AddChild(sensorHeader);

        _sensorSection = new VBoxContainer { Name = "SensorSection" };
        _layout.AddChild(_sensorSection);

        _layout.AddChild(new HSeparator());

        // Memory section
        _memoryHeader = new Label
        {
            Text = "Active Memories (0)",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _layout.AddChild(_memoryHeader);

        _memorySection = new VBoxContainer { Name = "MemorySection" };
        _layout.AddChild(_memorySection);

        _layout.AddChild(new HSeparator());

        // Log section
        var logHeader = new Label
        {
            Text = "Perception Log",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _layout.AddChild(logHeader);

        _logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _layout.AddChild(_logScroll);

        _logSection = new VBoxContainer { Name = "LogSection" };
        _logScroll.AddChild(_logSection);
    }

    private void PopulateSensors()
    {
        if (_manager == null) { return; }

        foreach (var sensor in _manager.RegisteredSensors)
        {
            var node = sensor.GetUnderlyingNode();
            var row = new HBoxContainer();

            var nameLabel = new Label
            {
                Text = node.Name,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            row.AddChild(nameLabel);

            var typeLabel = new Label
            {
                Text = $"({node.GetType().Name})"
            };
            row.AddChild(typeLabel);

            _sensorSection.AddChild(row);
        }
    }

    #endregion

    #region Memory Rows

    private MemoryRowCache CreateMemoryRow()
    {
        var row = new HBoxContainer();

        var nameLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(80, 0)
        };
        row.AddChild(nameLabel);

        var barBg = new ColorRect
        {
            Color = new Color(0.2f, 0.2f, 0.2f, 0.5f),
            CustomMinimumSize = new Vector2(ConfidenceBarMaxWidth, 12)
        };
        row.AddChild(barBg);

        var barFill = new ColorRect();
        barBg.AddChild(barFill);

        var confidenceLabel = new Label
        {
            CustomMinimumSize = new Vector2(40, 0)
        };
        row.AddChild(confidenceLabel);

        _memorySection.AddChild(row);

        return new MemoryRowCache
        {
            Row = row,
            NameLabel = nameLabel,
            BarBg = barBg,
            BarFill = barFill,
            ConfidenceLabel = confidenceLabel
        };
    }

    private void UpdateMemoryRow(MemoryRowCache cache, Perception3DInfo info)
    {
        var name = info.Identity?.IdentityName ?? info.Target?.Name ?? "Unknown";
        cache.NameLabel.Text = name;
        cache.BarFill.Color = ConfidenceColor(info.CurrentConfidence);
        cache.BarFill.CustomMinimumSize = new Vector2(info.CurrentConfidence * ConfidenceBarMaxWidth, 12);
        cache.ConfidenceLabel.Text = info.CurrentConfidence.ToString("F2");
    }

    private static Color ConfidenceColor(float confidence)
    {
        // Green (high) → Yellow (mid) → Red (low)
        if (confidence > 0.5f)
        {
            float t = (confidence - 0.5f) * 2f;
            return Colors.Yellow.Lerp(Colors.Green, t) with { A = 0.8f };
        }
        else
        {
            float t = confidence * 2f;
            return Colors.Red.Lerp(Colors.Yellow, t) with { A = 0.8f };
        }
    }

    #endregion

    #region Log

    private void Subscribe()
    {
        if (_manager == null || _subscribed) { return; }

        _manager.MemoryAddedEventHandler += OnMemoryAdded;
        _manager.MemoryForgottenEventHandler += OnMemoryForgotten;
        _subscribed = true;

        TreeExiting += Unsubscribe;
    }

    private void Unsubscribe()
    {
        if (_manager == null || !_subscribed) { return; }

        _manager.MemoryAddedEventHandler -= OnMemoryAdded;
        _manager.MemoryForgottenEventHandler -= OnMemoryForgotten;
        _subscribed = false;
    }

    private void OnMemoryAdded(object? sender, Perception3DInfo info)
    {
        var name = info.Identity?.IdentityName ?? info.Target?.Name ?? "Unknown";
        AddLogEntry($"Detected {name}");
    }

    private void OnMemoryForgotten(object? sender, Perception3DInfo info)
    {
        var name = info.Identity?.IdentityName ?? info.Target?.Name ?? "Unknown";
        AddLogEntry($"Forgot {name}");
    }

    private void AddLogEntry(string message)
    {
        var elapsed = (Time.GetTicksMsec() - _startTime) / 1000f;
        var entry = $"[+{elapsed:F2}] {message}";

        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > MaxLogEntries)
        {
            _logBuffer.Dequeue();
        }

        RebuildLogDisplay();
    }

    private void RebuildLogDisplay()
    {
        foreach (var child in _logSection.GetChildren())
        {
            _logSection.RemoveChild(child);
            child.Free();
        }

        foreach (var entry in _logBuffer)
        {
            var label = new Label { Text = entry };
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _logSection.AddChild(label);
        }

        // Scroll to bottom on next frame (after layout pass)
        if (IsInsideTree())
        {
            _logScroll.CallDeferred("set_v_scroll", (int)_logScroll.GetVScrollBar().MaxValue);
        }
    }

    #endregion

    #region Test Helpers
#if TOOLS
    internal string GetSensorDisplayName(int index)
    {
        if (index < 0 || index >= _sensorSection.GetChildCount()) { return string.Empty; }
        var row = _sensorSection.GetChild(index) as HBoxContainer;
        return row?.GetChild(0) is Label label ? label.Text : string.Empty;
    }

    internal string GetMemoryRowName(int index)
    {
        if (index < 0 || index >= _memoryRowPool.Count || !_memoryRowPool[index].Row.Visible) { return string.Empty; }
        return _memoryRowPool[index].NameLabel.Text;
    }

    internal float GetMemoryRowConfidence(int index)
    {
        if (index < 0 || index >= _memoryRowPool.Count || !_memoryRowPool[index].Row.Visible) { return -1f; }
        if (float.TryParse(_memoryRowPool[index].ConfidenceLabel.Text, out var val))
        {
            return val;
        }
        return -1f;
    }

    internal string GetLogEntryText(int index)
    {
        if (index < 0 || index >= _logBuffer.Count) { return string.Empty; }
        return _logBuffer.ElementAt(index);
    }

    internal void SimulateCleanup() => Unsubscribe();
#endif
    #endregion
}
