namespace Jmodot.Implementation.Components;

using System;
using System.Collections.Generic;
using Godot;
using AI.BB;
using Core.AI.BB;
using Core.Components;
using Core.Pooling;
using Core.Shared.Attributes;
using Core.Stats;
using Shared;
using GCol = Godot.Collections;
using StatAttribute = Core.Stats.Attribute;

/// <summary>
/// Generic size controller for any entity (spells, wizards, etc.).
/// Automatically discovers collision shapes, subscribes to size stat changes,
/// and scales all shapes + optional visual root.
///
/// CONFIGURATION:
/// - SizeAttribute: REQUIRED - must be set in editor (no default fallback)
/// - MinSize/MaxSize: Configurable bounds (defaults allow shrinking like spells)
///
/// BLACKBOARD REQUIREMENTS:
/// - BBDataSig.Stats (required): StatController containing the size attribute
/// - BBDataSig.Agent (optional): Node root for collision shape discovery
/// </summary>
[GlobalClass]
public partial class EntitySizeController : Node, IComponent, IPoolResetable
{
    /// <summary>
    /// Default minimum size - entities can shrink but shouldn't completely disappear.
    /// Set MinSize=1.0 to prevent shrinking (wizard behavior).
    /// </summary>
    public const float DEFAULT_MIN_SIZE = 0.25f;

    /// <summary>
    /// Default maximum size - prevents entities from growing too large.
    /// </summary>
    public const float DEFAULT_MAX_SIZE = 3.0f;

    // --- REQUIRED: Size Attribute ---
    [Export, RequiredExport]
    public StatAttribute SizeAttribute { get; set; } = null!;

    // --- Configurable Size Bounds ---
    [Export] public float MinSize { get; set; } = DEFAULT_MIN_SIZE;
    [Export] public float MaxSize { get; set; } = DEFAULT_MAX_SIZE;

    /// <summary>
    /// Parent nodes whose descendants should NOT be scaled.
    /// Use this to exclude entire branches (e.g., GrabberComponent3D for wizards).
    /// </summary>
    [Export] public GCol.Array<Node>? ExcludedParents { get; set; }

    /// <summary>
    /// Specific collision shapes that should NOT be scaled.
    /// Use this for fine-grained control over individual shapes.
    /// </summary>
    [Export] public GCol.Array<CollisionShape3D>? ExcludedShapes { get; set; }

    /// <summary>
    /// Optional visual root to scale along with collision shapes.
    /// </summary>
    [Export] public Node3D? VisualRoot { get; set; }

    private StatController? _stats;
    private List<ScalableShapeEntry> _scalableShapes = new();
    private Vector3 _baseVisualScale = Vector3.One;

    /// <summary>
    /// Represents a collision shape and its cached base scale for efficient scaling operations.
    /// </summary>
    public readonly record struct ScalableShapeEntry(CollisionShape3D Shape, Vector3 BaseScale);

    public bool IsInitialized { get; private set; }
    public event Action? Initialized;

    // --- Component Interface ---

    public bool Initialize(IBlackboard bb)
    {
        // Validate required SizeAttribute is configured
        if (SizeAttribute == null)
        {
            JmoLogger.Error(this, "EntitySizeController: SizeAttribute is required but not configured!");
            return false;
        }

        // Get StatController from Blackboard (required)
        if (!bb.TryGet(BBDataSig.Stats, out _stats) || _stats == null)
        {
            JmoLogger.Error(this, "EntitySizeController requires StatController in Blackboard!");
            return false;
        }

        // Get agent root for collision shape discovery (optional)
        bb.TryGet(BBDataSig.Agent, out Node agent);
        if (agent != null)
        {
            _scalableShapes = DiscoverScalableShapesWithBaseScales(agent, ExcludedParents, ExcludedShapes);

            if (_scalableShapes.Count > 0)
            {
                JmoLogger.Debug(this, $"Discovered {_scalableShapes.Count} collision shapes to scale");
            }
        }

        // Cache visual base scale
        if (VisualRoot != null)
        {
            _baseVisualScale = VisualRoot.Scale;
        }

        // Subscribe to size changes
        _stats.Subscribe(SizeAttribute, OnSizeChanged);

        // Apply initial size
        var currentSize = _stats.GetStatValue<float>(SizeAttribute, 1.0f);
        ApplyScale(currentSize);

        IsInitialized = true;
        Initialized?.Invoke();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;

    // --- Size Change Handler ---

    private void OnSizeChanged(Variant newValue)
    {
        if (newValue.VariantType == Variant.Type.Float)
        {
            ApplyScale(newValue.AsSingle());
        }
    }

    private void ApplyScale(float sizeMultiplier)
    {
        var clampedSize = SizeScalingUtils.ClampSize(sizeMultiplier, MinSize, MaxSize);

        // Scale all discovered collision shapes
        foreach (var entry in _scalableShapes)
        {
            entry.Shape.Scale = SizeScalingUtils.ApplyScale(entry.BaseScale, clampedSize);
        }

        // Scale visual root
        if (VisualRoot != null)
        {
            VisualRoot.Scale = SizeScalingUtils.ApplyScale(_baseVisualScale, clampedSize);
        }
    }

    // --- Cleanup ---

    public override void _ExitTree()
    {
        if (_stats != null && SizeAttribute != null)
        {
            _stats.Unsubscribe(SizeAttribute, OnSizeChanged);
        }
    }

    /// <summary>
    /// Resets the controller state for pool reuse. Called automatically via IPoolResetable.
    /// CRITICAL: Unsubscribes from stat changes to prevent subscription leaks.
    /// Without this, each pool reactivation would add another subscription,
    /// causing cumulative scale corruption (scale applied N times after N cycles).
    /// </summary>
    public void OnPoolReset()
    {
        // 1. Unsubscribe from stat changes (CRITICAL: prevents subscription leak)
        if (_stats != null && SizeAttribute != null)
        {
            _stats.Unsubscribe(SizeAttribute, OnSizeChanged);
        }

        // 2. Clear cached state
        _scalableShapes.Clear();
        _baseVisualScale = Vector3.One;

        // 3. Clear references
        _stats = null;

        // 4. Reset initialization flag
        IsInitialized = false;
    }

    #region Static Discovery Methods

    /// <summary>
    /// Discovers all CollisionShape3D nodes in the hierarchy that should be scaled.
    /// Filters out shapes based on ExcludedParents (entire branches) and ExcludedShapes (individual shapes).
    /// </summary>
    /// <param name="root">The root node to search from.</param>
    /// <param name="excludedParents">Parent nodes whose descendants should be excluded.</param>
    /// <param name="excludedShapes">Specific shapes to exclude.</param>
    /// <returns>List of collision shapes that should be scaled.</returns>
    public static List<CollisionShape3D> DiscoverScalableShapes(
        Node root,
        GCol.Array<Node>? excludedParents,
        GCol.Array<CollisionShape3D>? excludedShapes)
    {
        var result = new List<CollisionShape3D>();
        var allShapes = root.GetChildrenOfType<CollisionShape3D>(includeSubChildren: true);

        foreach (var shape in allShapes)
        {
            // Skip if shape is explicitly excluded
            if (excludedShapes != null && excludedShapes.Contains(shape))
            {
                continue;
            }

            // Skip if any ancestor is in ExcludedParents
            if (IsDescendantOfExcluded(shape, excludedParents))
            {
                continue;
            }

            result.Add(shape);
        }

        return result;
    }

    /// <summary>
    /// Discovers all scalable collision shapes and caches their base scales.
    /// </summary>
    /// <param name="root">The root node to search from.</param>
    /// <param name="excludedParents">Parent nodes whose descendants should be excluded.</param>
    /// <param name="excludedShapes">Specific shapes to exclude.</param>
    /// <returns>List of shapes with their cached base scales.</returns>
    public static List<ScalableShapeEntry> DiscoverScalableShapesWithBaseScales(
        Node root,
        GCol.Array<Node>? excludedParents,
        GCol.Array<CollisionShape3D>? excludedShapes)
    {
        var shapes = DiscoverScalableShapes(root, excludedParents, excludedShapes);
        var result = new List<ScalableShapeEntry>(shapes.Count);

        foreach (var shape in shapes)
        {
            result.Add(new ScalableShapeEntry(shape, shape.Scale));
        }

        return result;
    }

    /// <summary>
    /// Checks if a node is a descendant of any node in the excluded parents list.
    /// </summary>
    private static bool IsDescendantOfExcluded(Node node, GCol.Array<Node>? excludedParents)
    {
        if (excludedParents == null || excludedParents.Count == 0)
        {
            return false;
        }

        // Walk up the tree checking each ancestor
        var current = node.GetParent();
        while (current != null)
        {
            if (excludedParents.Contains(current))
            {
                return true;
            }
            current = current.GetParent();
        }

        return false;
    }

    #endregion
}
