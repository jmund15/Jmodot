namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using GCol = Godot.Collections;
using System.Collections.Generic;
using Shared;
using System.Linq;
using Core.Visual.Animation.Sprite; // Used for easier array checking in Editor logic

[Tool]
[GlobalClass]
public partial class AnimationVisibilityCoordinator : Node
{
    private NodePath _targetAnimatorPath;
    [Export] public NodePath TargetAnimatorPath
    {
        get => _targetAnimatorPath;
        set { _targetAnimatorPath = value; UpdateConfigurationWarnings(); }
    }

    private bool _autoRegisterNodes = true;
    [Export] public bool AutoRegisterNodes
    {
        get => _autoRegisterNodes;
        set { _autoRegisterNodes = value; UpdateConfigurationWarnings(); }
    }
    /// <summary>
    /// MANUALLY Maps an Animation Name (Base or Full) to a list of Nodes that should be VISIBLE when that animation plays.
    /// </summary>
    // TODO: implement manual map if needed
    [Export] public GCol.Dictionary<StringName, GCol.Array<NodePath>> ManualVisibilityMap { get; set; } = new();

    private string _nodePrefix = "Vis_";
    /// <summary>
    /// All nodes that should be auto-detected NEED to have this prefix!
    /// </summary>
    [Export] public string NodePrefix
    {
        get => _nodePrefix;
        set { _nodePrefix = value; UpdateConfigurationWarnings(); }
    }

    private string _suffixSeparator = "_";
    [Export] public string SuffixSeparator
    {
        get => _suffixSeparator;
        set { _suffixSeparator = value; UpdateConfigurationWarnings(); }
    }

    // Runtime Caches
    private Dictionary<StringName, List<Node>> _visibilityCache = new();
    private List<Node> _allManagedNodes = new();
    private IAnimComponent _targetAnimComponent = null!;

    public override void _Ready()
    {
        // Don't run runtime logic in the editor
        if (Engine.IsEditorHint()) return;

        if (AutoRegisterNodes)
        {
            RegisterChildrenRecursive(GetParent());
        }

        SetupAnimatorConnection();

        // ToDO: doesn't work????
        // if (Engine.IsEditorHint())
        // {
        //     GetTree().TreeChanged += UpdateConfigurationWarnings;
        // }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (Engine.IsEditorHint())
        {
            UpdateConfigurationWarnings();
        }
    }

    private void RegisterChildrenRecursive(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            // Check for BOTH CanvasItem (2D/UI) and Node3D (3D)
            bool isVisualNode = child is CanvasItem || child is Node3D;

            if (isVisualNode && child.Name.ToString().StartsWith(NodePrefix))
            {
                // Parse Name: "View_Run" -> "Run" (lowercased for consistency)
                string rawName = child.Name.ToString().Substring(NodePrefix.Length);
                StringName animKey = rawName.ToLower();

                if (!_visibilityCache.ContainsKey(animKey))
                {
                    _visibilityCache[animKey] = new List<Node>();
                }

                _visibilityCache[animKey].Add(child);
                _allManagedNodes.Add(child);

                SetNodeVisible(child, false);
            }
        }
    }

    private void SetupAnimatorConnection()
    {
        var target = GetNodeOrNull(TargetAnimatorPath);
        if (target is IAnimComponent animComp)
        {
            _targetAnimComponent = animComp;
            _targetAnimComponent.AnimStarted += OnAnimStarted;
        }
        else
        {
            JmoLogger.Error(this, $"TargetAnimatorPath '{TargetAnimatorPath}' is not an IAnimComponent!");
        }
    }

    private void OnAnimStarted(StringName animName)
    {
        // 1. Hide All
        foreach (var node in _allManagedNodes)
        {
            SetNodeVisible(node, false);
        }

        // 2. Normalize Input (run_left -> run)
        string nameStr = animName.ToString().ToLower();

        // Strip Suffix
        if (!string.IsNullOrEmpty(SuffixSeparator))
        {
            int idx = nameStr.LastIndexOf(SuffixSeparator);
            if (idx > 0) nameStr = nameStr.Substring(0, idx);
        }

        StringName key = new StringName(nameStr);

        // 3. Show Matching
        if (_visibilityCache.TryGetValue(key, out var nodesToShow))
        {
            foreach (var node in nodesToShow)
            {
                SetNodeVisible(node, true);
            }
        }
    }

    /// <summary>
    /// Helper to handle the fact that Node3D and CanvasItem don't share a 'Visible' interface.
    /// </summary>
    private void SetNodeVisible(Node node, bool isVisible)
    {
        if (node is Node3D n3d)
        {
            n3d.Visible = isVisible;
        }
        else if (node is CanvasItem ci)
        {
            ci.Visible = isVisible;
        }
    }

    // // do if needed for performance
    // private void UpdateConfigWarnings()
    // {
    //
    // }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (TargetAnimatorPath == null || TargetAnimatorPath.IsEmpty)
        {
            warnings.Add("TargetAnimatorPath is not set.");
            return warnings.ToArray();
        }

        var targetNode = GetNodeOrNull(TargetAnimatorPath);

        // 1. Check Interface
        if (targetNode is not IAnimComponent animComp)
        {
            warnings.Add($"Target node '{targetNode?.Name}' must implement IAnimComponent.");
            return warnings.ToArray();
        }

        // 2. Get List via Interface (Polymorphic!)
        string[] availableAnims = animComp.GetAnimationList();

        if (AutoRegisterNodes && availableAnims.Length > 0)
        {
            var parent = GetParent();
            if (parent != null)
            {
                foreach (var child in parent.GetChildren())
                {
                    // Only check nodes matching the prefix
                    string childName = child.Name.ToString();
                    if (!childName.StartsWith(NodePrefix)) continue;

                    string expectedKey = childName.Substring(NodePrefix.Length).ToLower();

                    bool matchFound = false;
                    foreach (var anim in availableAnims)
                    {
                        string processedAnim = anim.ToLower();

                        // Suffix Stripping Logic
                        if (!string.IsNullOrEmpty(SuffixSeparator))
                        {
                            int idx = processedAnim.LastIndexOf(SuffixSeparator);
                            if (idx > 0) processedAnim = processedAnim.Substring(0, idx);
                        }

                        if (processedAnim == expectedKey)
                        {
                            matchFound = true;
                            break;
                        }
                    }

                    if (!matchFound)
                    {
                        warnings.Add($"Node '{childName}' expects animation key '{expectedKey}', but the target IAnimComponent has no matching animation.");
                    }
                }
            }
        }
        return warnings.ToArray();
    }
}
