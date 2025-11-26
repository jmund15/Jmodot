namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using Godot.Collections;
using System;
using Core.Visual.Animation.Sprite;
using Shared;

/// <summary>
/// Listens to an animation component and toggles the visibility of nodes based on the current animation.
/// Solves the "One Animation Player, Multiple Sprites" problem.
/// Supports suffix stripping (e.g. mapping "run" covers "run_left", "run_right").
/// </summary>
[GlobalClass]
public partial class AnimationVisibilityCoordinator : Node
{
    /// <summary>
    /// Path to the IAnimComponent or AnimationPlayer to listen to.
    /// </summary>
    [Export] public NodePath TargetAnimatorPath { get; set; } = null!;

    /// <summary>
    /// Separator used to strip directional suffixes.
    /// </summary>
    [Export] public string SuffixSeparator { get; set; } = "_";

    /// <summary>
    /// Maps an Animation Name (Base or Full) to a list of Nodes that should be VISIBLE when that animation plays.
    /// </summary>
    [Export] public Dictionary<StringName, Array<NodePath>> VisibilityMap { get; set; } = new();

    /// <summary>
    /// List of ALL nodes managed by this coordinator.
    /// These nodes will be hidden if they are not in the active visibility set.
    /// </summary>
    [Export] public Array<NodePath> AllManagedNodes { get; set; } = new();

    private IAnimComponent _targetAnimComponent;
    private AnimationPlayer _targetAnimPlayer;

    public override void _Ready()
    {
        if (TargetAnimatorPath == null)
        {
            JmoLogger.Error(this, "TargetAnimatorPath is not set.");
            return;
        }

        var targetNode = GetNodeOrNull(TargetAnimatorPath);
        if (targetNode == null)
        {
            JmoLogger.Error(this, $"Target node at '{TargetAnimatorPath}' not found.");
            return;
        }

        // Try to bind to IAnimComponent first
        if (targetNode is IAnimComponent animComp)
        {
            _targetAnimComponent = animComp;
            _targetAnimComponent.AnimStarted += OnAnimStarted;
        }
        // Fallback to AnimationPlayer directly
        else if (targetNode is AnimationPlayer animPlayer)
        {
            _targetAnimPlayer = animPlayer;
            _targetAnimPlayer.AnimationStarted += OnAnimStarted;
        }
        else
        {
            JmoLogger.Error(this, $"Target node '{targetNode.Name}' is neither IAnimComponent nor AnimationPlayer.");
        }
    }

    private void OnAnimStarted(StringName animName)
    {
        UpdateVisibility(animName);
    }

    private void UpdateVisibility(StringName animName)
    {
        string nameStr = animName.ToString();

        // 1. Resolve the list of nodes to show
        Array<NodePath> nodesToShow = null;

        // A. Direct Match
        if (VisibilityMap.ContainsKey(animName))
        {
            nodesToShow = VisibilityMap[animName];
        }
        // B. Suffix Stripped Match
        else if (!string.IsNullOrEmpty(SuffixSeparator))
        {
            int lastSepIndex = nameStr.LastIndexOf(SuffixSeparator, StringComparison.Ordinal);
            if (lastSepIndex > 0)
            {
                string baseName = nameStr.Substring(0, lastSepIndex);
                if (VisibilityMap.ContainsKey(baseName))
                {
                    nodesToShow = VisibilityMap[baseName];
                }
            }
        }

        // 2. Apply Visibility
        foreach (var path in AllManagedNodes)
        {
            var node = GetNodeOrNull(path);
            if (node == null) continue;

            bool shouldShow = false;
            if (nodesToShow != null && nodesToShow.Contains(path))
            {
                shouldShow = true;
            }

            SetNodeVisibility(node, shouldShow);
        }
    }

    private void SetNodeVisibility(Node node, bool visible)
    {
        if (node is Node3D node3D)
        {
            node3D.Visible = visible;
        }
        else if (node is CanvasItem canvasItem)
        {
            canvasItem.Visible = visible;
        }
    }

    public override void _ExitTree()
    {
        if (_targetAnimComponent != null) _targetAnimComponent.AnimStarted -= OnAnimStarted;
        if (_targetAnimPlayer != null) _targetAnimPlayer.AnimationStarted -= OnAnimStarted;
    }
}
