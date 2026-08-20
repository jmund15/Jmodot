namespace Jmodot.Implementation.Body.Segmented;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.AI.BB;
using Core.Components;
using Core.Health;
using Core.Visual.Animation.Sprite;
using Implementation.AI.BB;
using Implementation.Health;
using Implementation.Shared;
using Implementation.Shared.GodotExceptions;

/// <summary>
/// One unit of a segmented body. Holds only its own local state — where it sits in the chain, who
/// owns it, and which way it is currently told to face — and knows nothing about the units either
/// side of it.
/// </summary>
/// <remarks>
/// <para>
/// Required blackboard key: <see cref="BBDataSig.HealthComponent"/>. A segment that cannot report
/// its own death cannot be split around, so its absence is a configuration warning in the editor
/// and a throw at <see cref="Initialize"/> — never a per-use warning. Optional:
/// <see cref="BBDataSig.AnimationOrchestrator"/>, without which <see cref="Facing"/> is recorded
/// but drives no clip.
/// </para>
/// <para>
/// A host binds and unbinds this unit; the unit never binds itself and never reparents. Its world
/// pose is written by its host, so nothing here drives movement.
/// </para>
/// </remarks>
[GlobalClass, Tool]
public partial class BodySegment3D : Node3D, IComponent, IBlackboardProvider
{
    private Vector3 _facing = Vector3.Forward;
    private IHealth? _health;
    private IAnimationOrchestrator? _orchestrator;

    /// <summary>Position in the chain: 0 is the unit directly behind the head. Meaningful only until the next death in the same frame.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>The chain that owns this unit's pose, or null while it is unbound.</summary>
    public SegmentedBodyComponent3D? Host { get; private set; }

    /// <summary>
    /// The direction this unit is travelling, written by its host every frame. Assigning it pushes
    /// the direction to this unit's own animation orchestrator.
    /// </summary>
    public Vector3 Facing
    {
        get => this._facing;
        set
        {
            this._facing = value;
            this._orchestrator?.SetDirection(value);
        }
    }

    /// <summary>This unit's own health. Required — resolved from its own blackboard at initialization.</summary>
    public IHealth Health => this._health
        ?? throw new InvalidOperationException($"{nameof(BodySegment3D)} was read before initialization resolved its health.");

    /// <inheritdoc />
    public (StringName Key, object Value)? Provision => (BBDataSig.BodySegment, this);

    /// <summary>Raised once when this unit dies, forwarded from its own health.</summary>
    public event Action<BodySegment3D>? Died;

    /// <summary>Claims this unit for <paramref name="host"/> at <paramref name="index"/>. Re-binding an already-bound unit re-indexes it.</summary>
    public void BindHost(SegmentedBodyComponent3D host, int index)
    {
        this.Host = host;
        this.Index = index;
    }

    /// <summary>Releases this unit from its host. The unit keeps its pose; nothing frees it.</summary>
    public void Unbind()
    {
        this.Host = null;
        this.Index = -1;
    }

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();
        var missingHealth = ConfigWarnings.RequireEntitySibling<HealthComponent>(this,
            "No HealthComponent on this segment — a segment that cannot report its own death fails to initialize.");
        if (missingHealth != null)
        {
            warnings.Add(missingHealth);
        }

        return warnings.Concat(base._GetConfigurationWarnings() ?? []).ToArray();
    }

    #region IComponent

    public bool IsInitialized { get; private set; }

    public event Action Initialized = delegate { };

    /// <exception cref="NodeConfigurationException">The segment's blackboard carries no health.</exception>
    public bool Initialize(IBlackboard bb)
    {
        // Teardown-first: every phase may run again on the same instance (scene rebind, pool reuse).
        if (this._health != null) { this._health.OnDied -= this.OnHealthDied; }

        if (!bb.TryGet<IHealth>(BBDataSig.HealthComponent, out var health) || health == null)
        {
            throw new NodeConfigurationException(
                "A body segment requires a HealthComponent on its own blackboard.", this);
        }

        this._health = health;
        bb.TryGet<IAnimationOrchestrator>(BBDataSig.AnimationOrchestrator, out this._orchestrator);

        this.IsInitialized = true;
        this.Initialized();
        return true;
    }

    public void OnPostInitialize()
    {
        if (this._health == null) { return; }

        this._health.OnDied -= this.OnHealthDied;
        this._health.OnDied += this.OnHealthDied;
    }

    public Node GetUnderlyingNode() => this;

    #endregion

    private void OnHealthDied(HealthChangeEventArgs _) => this.Died?.Invoke(this);

    #region Test Helpers
#if TOOLS
    internal void _TestSetOrchestrator(IAnimationOrchestrator? orchestrator) => this._orchestrator = orchestrator;
#endif
    #endregion
}
