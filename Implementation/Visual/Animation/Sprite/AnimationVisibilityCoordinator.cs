namespace Jmodot.Implementation.Visual.Animation.Sprite;

using Godot;
using GCol = Godot.Collections;
using System;
using System.Collections.Generic;
using Shared;
using System.Linq;
using Core.Visual.Animation.Sprite;
using Core.Visual.Effects;

[Tool]
[GlobalClass]
public partial class AnimationVisibilityCoordinator : Node, IVisualSpriteProvider
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
    /// <summary>
    /// Allows names such as 'Vis_Run_Front' and 'Vis_Run_Back' (assuming 'Vis' is the 'NodePrefix') to both trigger when the animation 'Run' plays!
    /// </summary>
    [Export] public bool IgnoreNodeNameAfterUnderscore { get; set; } = true;


    private string _animNameSuffixSeparator = "_";
    [Export] public string AnimNameSuffixSeparator
    {
        get => _animNameSuffixSeparator;
        set { _animNameSuffixSeparator = value; UpdateConfigurationWarnings(); }
    }


    // Runtime Caches
    private Dictionary<StringName, List<Node>> _visibilityCache = new();
    private List<Node> _allManagedNodes = new();
    private IAnimComponent _targetAnimComponent = null!;
    private bool _subscribedToParentChildEnteredTree;

    /// <summary>
    /// Extracts the animation lookup key from a node name by stripping the prefix
    /// and optionally removing the suffix after the first underscore.
    /// e.g. "Vis_PotionAdd_Front" → "potionadd"
    /// </summary>
    private StringName ExtractAnimKeyFromNodeName(string nodeName)
    {
        string rawName = nodeName.Substring(NodePrefix.Length);
        if (IgnoreNodeNameAfterUnderscore)
        {
            var suffixLoc = rawName.Find('_');
            if (suffixLoc != -1)
            {
                rawName = rawName.Substring(0, suffixLoc);
            }
        }
        return rawName.ToLower();
    }

    /// <summary>
    /// Fired when the set of visible nodes changes (animation change, node added/removed).
    /// </summary>
    public event Action VisibleNodesChanged = delegate { };
    public event Action VisualNodesChanged = delegate { };

    public override void _Ready()
    {
        // Don't run runtime logic in the editor
        if (Engine.IsEditorHint()) { return; }

        if (AutoRegisterNodes)
        {
            RegisterChildren(GetParent());

            // Connect to ChildEnteredTree to detect dynamically added nodes
            var parent = GetParent();
            if (parent != null)
            {
                parent.ChildEnteredTree += OnChildEnteredTree;
                _subscribedToParentChildEnteredTree = true;
            }
        }

        SetupAnimatorConnection();

        // ToDO: doesn't work????
        if (Engine.IsEditorHint() && Engine.GetMainLoop() is SceneTree sceneTree)
        {
            sceneTree.TreeChanged += UpdateConfigurationWarnings;
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (!Engine.IsEditorHint())
        {
            if (_subscribedToParentChildEnteredTree)
            {
                var parent = GetParent();
                if (parent != null && IsInstanceValid(parent))
                {
                    parent.ChildEnteredTree -= OnChildEnteredTree;
                }
                _subscribedToParentChildEnteredTree = false;
            }
            if (_targetAnimComponent != null)
            {
                _targetAnimComponent.AnimStarted -= OnAnimStarted;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (Engine.IsEditorHint())
        {
            UpdateConfigurationWarnings();
        }
    }

    /// <summary>
    /// Called when a new child is added to the parent at runtime.
    /// Registers the new node if it matches our criteria.
    /// </summary>
    private void OnChildEnteredTree(Node node)
    {
        if (Engine.IsEditorHint()) { return; }

        //GD.Print($"[AnimVis] ChildEnteredTree fired for: {node.Name}");

        // Register the new node and all its children recursively
        RegisterChildren(node);

        // If we're currently playing an animation, update visibility for the new nodes
        if (_targetAnimComponent != null && _targetAnimComponent.IsPlaying())
        {
            var currentAnim = _targetAnimComponent.GetCurrAnimation();
            //GD.Print($"[AnimVis] Animation is playing: {currentAnim}, calling OnAnimStarted");
            OnAnimStarted(currentAnim);
        }
        else
        {
            //GD.Print($"[AnimVis] No animation playing, skipping visibility update");
        }
    }

    private void RegisterChildren(Node parent)
    {
        // First, check if the parent itself is a visual node
        RegisterNodeIfValid(parent);

        // Then check all children
        foreach (var child in parent.GetChildrenOfType<Node>())
        {
            RegisterNodeIfValid(child);
        }
    }

    /// <summary>
    /// Checks if a node should be registered and adds it to the visibility system.
    /// </summary>
    private void RegisterNodeIfValid(Node node)
    {
        // Check for BOTH CanvasItem (2D/UI) and Node3D (3D)
        bool isVisualNode = node is CanvasItem || node is Node3D;

        if (isVisualNode && node.Name.ToString().StartsWith(NodePrefix))
        {
            // Prevent duplicate registration
            if (_allManagedNodes.Contains(node))
            {
                //GD.Print($"[AnimVis] Skipping duplicate registration: {node.Name}");
                return;
            }

            StringName animKey = ExtractAnimKeyFromNodeName(node.Name.ToString());

            if (!_visibilityCache.ContainsKey(animKey))
            {
                _visibilityCache[animKey] = new List<Node>();
            }

            _visibilityCache[animKey].Add(node);
            _allManagedNodes.Add(node);

            //GD.Print($"[AnimVis] Registered node: {node.Name} with key: {animKey}");

            // Initially hide new nodes - they'll be shown if they match the current animation
            SetNodeVisible(node, false);
            VisualNodesChanged.Invoke();
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
        if (!string.IsNullOrEmpty(AnimNameSuffixSeparator))
        {
            int idx = nameStr.LastIndexOf(AnimNameSuffixSeparator);
            if (idx > 0) { nameStr = nameStr.Substring(0, idx); }
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

        // Notify listeners that visible nodes have changed
        VisibleNodesChanged.Invoke();
        //GD.Print($"[AnimVis] Animation started: {animName}, showing nodes: {nodesToShow.Count}");
    }

    #region IVisualSpriteProvider Implementation

    /// <summary>
    /// Returns all currently VISIBLE managed nodes.
    /// </summary>
    public IReadOnlyList<Node> GetVisibleNodes()
    {
        return _allManagedNodes.Where(IsNodeVisible).ToList();
    }

    public IReadOnlyList<Node> GetAllVisualNodes()
    {
        return _allManagedNodes;
    }

    private bool IsNodeVisible(Node node)
    {
        return (node is Node3D n3d && n3d.Visible) || (node is CanvasItem ci && ci.Visible);
    }

    #endregion

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
                foreach (var child in parent.GetChildrenOfType<Node>())
                {
                    // Only check nodes matching the prefix
                    string childName = child.Name.ToString();
                    if (!childName.StartsWith(NodePrefix)) { continue; }

                    string expectedKey = ExtractAnimKeyFromNodeName(childName);

                    bool matchFound = false;
                    foreach (var anim in availableAnims)
                    {
                        string processedAnim = anim.ToLower();

                        // Suffix Stripping Logic
                        if (!string.IsNullOrEmpty(AnimNameSuffixSeparator))
                        {
                            int idx = processedAnim.LastIndexOf(AnimNameSuffixSeparator);
                            if (idx > 0) { processedAnim = processedAnim.Substring(0, idx); }
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
                        JmoLogger.Error(this, $"Node '{childName}' expects animation key '{expectedKey}', but the target IAnimComponent has no matching animation.");
                    }
                }
            }
        }
        return warnings.ToArray();
    }
}
