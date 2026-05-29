namespace Jmodot.Implementation.Physics.Collision;

using System;
using Godot;
using Jmodot.Core.AI.BB;
using Jmodot.Core.Components;
using Jmodot.Core.Physics;
using Jmodot.Core.Pooling;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Core.Stats;
using Jmodot.Implementation.AI.BB;
using Jmodot.Implementation.Combat;

/// <summary>
/// Node3D collision responder component. Owns a <see cref="CollisionResponderCore"/>, builds it
/// from an authored <see cref="CollisionResponseConfig"/> plus the runtime stat provider resolved
/// from <c>BBDataSig.Stats</c>, and forwards the <see cref="ICollisionResponder"/> surface to it.
///
/// Standard <see cref="IComponent"/> lifecycle: silently no-ops (persists) until
/// <see cref="Initialize"/> runs. On pool reuse, <see cref="OnPoolReset"/> clears the core's
/// full mutable state.
///
/// Required blackboard keys: <c>BBDataSig.Stats</c> (optional — a host with no stat system still
/// resolves; stat-driven counts/modifiers degrade to their unset defaults).
/// </summary>
[GlobalClass]
public partial class CollisionResponderComponent3D : Node3D, IComponent, IPoolResetable, ICollisionResponder
{
    [Export, RequiredExport] public CollisionResponseConfig Config { get; set; } = null!;
    [Export] public HitboxComponent3D? Hitbox { get; set; }

    private readonly CollisionResponderCore _core = new();

    #region IComponent

    public bool IsInitialized { get; private set; }

    public event Action Initialized = delegate { };

    public override void _Ready()
    {
        this.ValidateRequiredExports();
    }

    public bool Initialize(IBlackboard bb)
    {
        if (IsInitialized) { return true; }

        bb.TryGet(BBDataSig.Stats, out IStatProvider? stats);

        _core.Initialize(
            Config.CategoryResponses,
            Config.DefaultResponse,
            Config.UseNormalFallback,
            Config.GroundCategory,
            Config.WallCategory,
            Config.BounceStrategy,
            Config.PierceStrategy as PiercePhysicsStrategy,
            Config.SlideStrategy as SlidePhysicsStrategy,
            Config.ExemptLayers,
            Config.GravityScaleAttribute,
            Config.PostBounceGravityMultiplier,
            Config.BounceSpeedAttribute,
            stats);

        IsInitialized = true;
        Initialized();
        OnPostInitialize();
        return true;
    }

    public void OnPostInitialize() { }

    public Node GetUnderlyingNode() => this;

    #endregion

    #region IPoolResetable

    public void OnPoolReset() => _core.Reset();

    #endregion

    #region ICollisionResponder

    public bool HandleCollision(ICollisionHost host, CollisionContact contact)
    {
        if (!IsInitialized) { return true; }
        return _core.HandleCollision(host, contact);
    }

    public bool HandleCollisionWithResponse(ICollisionHost host, CollisionContact contact, BaseCollisionResponse response)
    {
        if (!IsInitialized) { return true; }
        return _core.HandleCollisionWithResponse(host, contact, response);
    }

    public void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox)
    {
        if (!IsInitialized) { return; }
        _core.ConfigureBody(host, hitbox);
    }

    #endregion
}
