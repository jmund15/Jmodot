namespace Jmodot.Implementation.Movement.Forces;

using Godot;
using Core.AI.BB;
using Core.Environment;
using Core.Stats;
using Implementation.AI.BB;
using Actors;
using Shared;
using Shared.GodotExceptions;

/// <summary>
/// A reusable force provider that applies gravity to ground-based CharacterBody3D entities
/// via the ExternalForceReceiver3D pipeline. Returns CharacterBody3D.GetGravity() when the
/// body is not on the floor, and Vector3.Zero when grounded.
///
/// <para><b>v6.2: stat-driven gravity scaling.</b> When
/// <see cref="GravityScaleAttribute"/> is wired, the raw gravity is multiplied by the
/// entity's stat value for that Attribute, read via <see cref="BBDataSig.Stats"/> →
/// <see cref="IStatProvider"/>. This lets designers tune fall-speed per archetype on
/// the stat sheet (heavy enemies fall fast, light/floaty enemies fall slowly). Wires
/// the same <c>gravity_scale</c> attribute spells use (see e.g. fireball_statsheet.tres
/// or <c>DelayedGravityActivationEffectInstance</c> for the spell-side write pattern).
/// When the export is null OR the BB / stats lookup fails, the scale defaults to 1.0
/// (back-compat — entities that don't opt in see raw gravity).
/// </para>
///
/// <para>Scene tree expects:</para>
/// <code>
///   CharacterBody3D
///   ├── Blackboard (or any IBlackboard implementation) — sibling
///   └── ExternalForceReceiver3D (Area3D)
///       └── GravityForceProvider3D (this node)
/// </code>
///
/// <para><b>Composition with status effects:</b> the gravity_scale stat is a regular
/// <see cref="IStatProvider"/> stat, so future Heavy/Levitate status effects can apply
/// <c>FloatAttributeModifier</c>s to it via the standard modifier pipeline (compose,
/// don't override — per <c>Modifier_Pipeline_Consistency</c>). The provider reads the
/// final modified value at every physics tick.</para>
/// </summary>
[GlobalClass]
public partial class GravityForceProvider3D : Node, IForceProvider3D
{
    /// <summary>
    /// Optional <c>gravity_scale</c> Attribute. When wired, the entity's IStatProvider
    /// value for this Attribute multiplies the raw GetGravity() output. When null, the
    /// scale defaults to 1.0 (back-compat — no stat-driven scaling).
    /// </summary>
    [Export] public Core.Stats.Attribute? GravityScaleAttribute { get; set; }

    private CharacterBody3D _body = null!;
    private IBlackboard? _bb;
    // Lazily-cached IStatProvider — avoids the per-physics-tick BB.TryGet dictionary
    // lookup on N grounded entities × 60Hz. Resolved on first ResolveGravityScale call
    // (after _Ready has populated _bb). Provider identity is stable through the entity's
    // lifetime; modifier composition still happens inside the IStatProvider itself,
    // not via re-resolution. See BB_Apply_Cache_Restore_Pattern.
    private IStatProvider? _cachedStats;
    private bool _statsResolved;

    public override void _Ready()
    {
        // Walk up: this → ExternalForceReceiver3D → CharacterBody3D
        var receiver = GetParent() as ExternalForceReceiver3D
            ?? throw new NodeConfigurationException(
                "GravityForceProvider3D must be a direct child of ExternalForceReceiver3D.", this);

        var parent = receiver.GetParent();
        _body = parent as CharacterBody3D
            ?? throw new NodeConfigurationException(
                "GravityForceProvider3D requires a CharacterBody3D grandparent.", this);

        receiver.RegisterInternalProvider(this);

        // v6.2: optional BB resolution for stat-driven gravity scale. Walk the body's
        // direct children to find any IBlackboard sibling — Blackboard.cs is the typical
        // implementation but any IBlackboard-implementing Node qualifies. Logged once at
        // _Ready so misconfiguration surfaces at scene-load.
        if (GravityScaleAttribute != null)
        {
            foreach (var child in _body.GetChildren())
            {
                if (child is IBlackboard bb)
                {
                    _bb = bb;
                    break;
                }
            }
            if (_bb == null)
            {
                JmoLogger.Warning(this,
                    "GravityScaleAttribute is set but no IBlackboard sibling was found on the "
                    + "parent CharacterBody3D. Stat-driven gravity scaling disabled — falling "
                    + "back to raw GetGravity().");
            }
        }
    }

    public Vector3 GetForceFor(Node3D target)
    {
        return IsGrounded() ? Vector3.Zero : GetGravityVector() * ResolveGravityScale();
    }

    protected virtual bool IsGrounded() => _body.IsOnFloor();
    protected virtual Vector3 GetGravityVector() => _body.GetGravity();

    /// <summary>
    /// Resolves the gravity-scale multiplier to apply to the raw gravity vector.
    /// Defaults to 1.0 when (a) no Attribute is wired, (b) the BB lookup failed at
    /// _Ready, or (c) the BB.Stats key isn't registered or the StatProvider doesn't
    /// hold the attribute. The IStatProvider reference is cached on first resolution;
    /// runtime modifiers (status effects, state-driven multipliers) still compose
    /// naturally because composition happens inside the provider, not via re-resolution.
    /// </summary>
    protected virtual float ResolveGravityScale()
    {
        if (GravityScaleAttribute == null || _bb == null) { return 1.0f; }
        if (!_statsResolved)
        {
            if (_bb.TryGet<IStatProvider>(BBDataSig.Stats, out var stats) && stats != null)
            {
                _cachedStats = stats;
            }
            _statsResolved = true;
        }
        if (_cachedStats == null) { return 1.0f; }
        return _cachedStats.GetStatValue<float>(GravityScaleAttribute, 1.0f);
    }

    #region Test Helpers
#if TOOLS
    internal void SetBody(CharacterBody3D body) => _body = body;
    internal void SetBlackboardForTesting(IBlackboard bb)
    {
        _bb = bb;
        // Invalidate the lazy IStatProvider cache so the next ResolveGravityScale
        // call re-resolves against the new BB (matches test-fixture re-wiring patterns).
        _cachedStats = null;
        _statsResolved = false;
    }
    internal void SetGravityScaleAttributeForTesting(Core.Stats.Attribute attr)
        => this.GravityScaleAttribute = attr;
#endif
    #endregion
}
