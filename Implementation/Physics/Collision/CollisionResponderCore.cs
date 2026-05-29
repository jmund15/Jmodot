namespace Jmodot.Implementation.Physics.Collision;

using System.Collections.Generic;
using Godot;
using Jmodot.Core.Identification;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.StageRules;
using Jmodot.Core.Physics;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Combat;
using Attr = Jmodot.Core.Stats.Attribute;
using GCol = Godot.Collections;

/// <summary>
/// Host-independent collision responder. Dispatches collision handling based on
/// Category-keyed response mappings; works with any entity implementing
/// <see cref="ICollisionHost"/> (spells, ingredients, thrown objects, etc.).
///
/// Plain (non-Node) class so it can be owned by either a Node component
/// (<c>CollisionResponderComponent3D</c>) or a thin runner shell. All mutable state is a
/// reset obligation — see <see cref="Reset"/> for pool reuse.
///
/// Resolution order: exempt layers → category match (Identity) → normal fallback
/// (infer Ground/Wall) → DefaultResponse. Guards: per-frame bounce coalesce, pierce debounce.
/// Post-collision stat modifiers: one-time gravity + compounding speed (bounce), compounding
/// speed (pierce). Self-damage routed via <see cref="ICollisionHost.TakeDamage"/> (health-less
/// hosts no-op, preserving the "no health → no self-damage" contract).
/// </summary>
public sealed class CollisionResponderCore : ICollisionResponder
{
    private GCol.Array<CategoryResponseMapping> _categoryResponses = new();
    private BaseCollisionResponse? _defaultResponse;
    private bool _useNormalFallback;
    private Category? _groundCategory;
    private Category? _wallCategory;

    // Response-keyed count tracking (shared counter when same response object reused)
    private readonly Dictionary<DurableCollisionResponse, int> _remainingCounts = new();
    private readonly Dictionary<BaseCollisionResponse, BaseCollisionResponse> _activeFallbacks = new();

    private ICollisionPhysicsStrategy? _bounceStrategy;
    private PiercePhysicsStrategy? _pierceStrategy;
    private SlidePhysicsStrategy? _slideStrategy;

    // Frame coalescing — prevents double-counting at corners (Bounce only)
    private ulong _lastBounceFrame = ulong.MaxValue;
    internal ulong? _testFrameOverride;

    // Pierce debounce — prevents sustained contact from burning pierce counts
    public const double DEBOUNCE_TIME_SECONDS = 0.1;
    private readonly Dictionary<Node, double> _lastPierceHitTimes = new();
    internal double? _testTimeOverride;

    // Stat provider override for testing (avoids concrete StatController dependency)
    internal IStatProvider? _testStatProviderOverride;

    // Layer-based exemption — colliders on these layers are ignored entirely
    private uint _exemptLayers;

    // Post-bounce gravity modifier flag (applied once)
    private bool _gravityModified;

    // Factory-level post-bounce stat modifier config (strategy-agnostic)
    private Attr? _gravityScaleAttribute;
    private float _postBounceGravityMultiplier = 1f;
    private Attr? _bounceSpeedAttribute;

    // Stat provider resolved from the host at configuration time (BBDataSig.Stats / CollisionStatProvider).
    private IStatProvider? _statProvider;

    /// <summary>
    /// Configures the responder. Entity-agnostic — the owning component or runner shell
    /// resolves the stat provider and strategies from its own source (config Resource or
    /// factory args) and forwards them here.
    /// </summary>
    public void Initialize(
        GCol.Array<CategoryResponseMapping> categoryResponses,
        BaseCollisionResponse? defaultResponse,
        bool useNormalFallback,
        Category? groundCategory,
        Category? wallCategory,
        ICollisionPhysicsStrategy? bounceStrategy,
        PiercePhysicsStrategy? pierceStrategy,
        SlidePhysicsStrategy? slideStrategy,
        uint exemptLayers,
        Attr? gravityScaleAttribute,
        float postBounceGravityMultiplier,
        Attr? bounceSpeedAttribute,
        IStatProvider? statProvider)
    {
        _categoryResponses = categoryResponses ?? new();
        _defaultResponse = defaultResponse;
        _useNormalFallback = useNormalFallback;
        _groundCategory = groundCategory;
        _wallCategory = wallCategory;
        _bounceStrategy = bounceStrategy;
        _pierceStrategy = pierceStrategy;
        _slideStrategy = slideStrategy;
        _exemptLayers = exemptLayers;
        _gravityScaleAttribute = gravityScaleAttribute;
        _postBounceGravityMultiplier = postBounceGravityMultiplier;
        _bounceSpeedAttribute = bounceSpeedAttribute;
        _statProvider = statProvider;

        RegisterAllCounts();
    }

    /// <summary>
    /// Clears all mutable per-instance state then re-registers counts. Call on pool reuse so a
    /// recycled host starts fresh. The bounce-frame sentinel resets to <see cref="ulong.MaxValue"/>
    /// (NOT 0 — a frame-0 bounce after reuse would otherwise be coalesced/swallowed).
    /// </summary>
    public void Reset()
    {
        _remainingCounts.Clear();
        _activeFallbacks.Clear();
        _lastPierceHitTimes.Clear();
        _lastBounceFrame = ulong.MaxValue;
        _gravityModified = false;
        RegisterAllCounts();
    }

    public bool HandleCollision(ICollisionHost host, CollisionContact contact)
    {
        // 0. EXEMPT LAYERS — persist without any processing
        if (_exemptLayers != 0 && contact.Collider is CollisionObject3D obj
            && (obj.CollisionLayer & _exemptLayers) != 0)
        {
            return true;
        }

        var response = ResolveResponse(contact);
        if (response == null)
        {
            return false;
        }

        return HandleCollisionWithResponse(host, contact, response);
    }

    public void ConfigureBody(ICollisionHost host, HitboxComponent3D? hitbox)
    {
        _bounceStrategy?.ConfigureBody(host, hitbox);
        _pierceStrategy?.ConfigureBody(host, hitbox);
        _slideStrategy?.ConfigureBody(host, hitbox);
    }

    public bool HandleCollisionWithResponse(ICollisionHost host, CollisionContact contact, BaseCollisionResponse response)
    {
        // DESTROY — immediate exit, no properties needed
        if (response is DestroyCollisionResponse)
        {
            return false;
        }

        // Pattern match for durable responses — gracefully handles unknown future types
        if (response is not DurableCollisionResponse durable)
        {
            return false; // Unknown response type — destroy as fallback
        }

        // 1. BOUNCE FRAME COALESCE — prevent duplicate bounces per physics frame (corners).
        //    Must run BEFORE count check: a corner collision on the same frame as a legitimate
        //    bounce must be coalesced, not rejected as "count exhausted."
        if (durable is BounceCollisionResponse)
        {
            ulong currentFrame = _testFrameOverride ?? Engine.GetPhysicsFrames();
            if (currentFrame == _lastBounceFrame)
            {
                return true; // Persist without consuming count, self-damage, or physics
            }
        }

        // 2. PIERCE DEBOUNCE — prevent sustained contact from consuming counts
        if (durable is PierceCollisionResponse && IsSustainedPierceContact(contact))
        {
            return true; // Persist without consuming count, self-damage, or physics
        }

        // 3. COUNT CHECK — if exhausted, try fallback recursion
        // Resolve count from definition (returns -1 for unlimited when no definition set)
        int maxCount = durable.ResolveMaxCount(GetStatProvider());
        if (maxCount >= 0)
        {
            if (!_remainingCounts.TryGetValue(durable, out int remaining) || remaining <= 0)
            {
                if (durable.FallbackResponse != null)
                {
                    ActivateFallback(durable, durable.FallbackResponse);
                    return HandleCollisionWithResponse(host, contact, durable.FallbackResponse);
                }
                return false;
            }
        }

        // Hoist impact speed once — used by both 3.5 (velocity fallback) and 4 (min threshold).
        float impactSpeed = host.Controller.PreMoveVelocity.Length();

        // 3.5. VELOCITY-DRIVEN FALLBACK — if impact speed is below the configured
        //     fallback threshold, swap to FallbackResponse (sticky per-mapping, same
        //     mechanism as count exhaustion). Enables transitions like Bounce → Slide
        //     as a spell decelerates.
        if (durable.VelocityFallbackThreshold > 0f && durable.FallbackResponse != null
            && !_activeFallbacks.ContainsKey(durable)
            && impactSpeed < durable.VelocityFallbackThreshold)
        {
            ActivateFallback(durable, durable.FallbackResponse);
            return HandleCollisionWithResponse(host, contact, durable.FallbackResponse);
        }

        // 4. VELOCITY THRESHOLD — skip if impact velocity too low
        if (durable.MinVelocityThreshold > 0f && impactSpeed < durable.MinVelocityThreshold)
        {
            return true; // Persist without consuming count, self-damage, or physics
        }

        // 5. DISPATCH PHYSICS — apply the physics strategy.
        var physicsResult = DispatchPhysics(host, contact, durable);
        if (physicsResult == PhysicsApplyResult.Failed)
        {
            return false;
        }

        // Only run post-physics steps (count, stat mods, self-damage) when physics
        // were actually applied. Skipped means the strategy persisted without changing
        // velocity (e.g., closingSpeed ≤ 0) — don't consume count or apply side effects.
        if (physicsResult == PhysicsApplyResult.Applied)
        {
            // 6. DECREMENT COUNT
            if (maxCount >= 0 && _remainingCounts.ContainsKey(durable))
            {
                _remainingCounts[durable]--;
            }

            // 7. RECORD BOUNCE FRAME — for next-frame coalescing check
            if (durable is BounceCollisionResponse)
            {
                _lastBounceFrame = _testFrameOverride ?? Engine.GetPhysicsFrames();
            }

            // 8. RECORD PIERCE HIT TIME — for next-contact debounce check
            if (durable is PierceCollisionResponse)
            {
                RecordPierceHit(contact);
            }

            // 9. APPLY BOUNCE STAT MODIFIERS — gravity (once) + speed (per-bounce)
            ApplyBounceStatModifiers(durable);

            // 10. APPLY PIERCE STAT MODIFIERS — speed (per-pierce)
            ApplyPierceStatModifiers(durable);

            // 11. APPLY SELF-DAMAGE — health-less hosts no-op via IDamageable, matching the
            //     legacy "host.Health != null" gate's observable behavior.
            float selfDamage = ResolveSelfDamage(host, durable);
            if (selfDamage > 0)
            {
                host.TakeDamage(selfDamage, this);
            }
        }

        // 12. return true (persist)
        return true;
    }

    private PhysicsApplyResult DispatchPhysics(
        ICollisionHost host, CollisionContact contact, DurableCollisionResponse response)
    {
        return response switch
        {
            BounceCollisionResponse => _bounceStrategy?.Apply(host, contact, response.VelocityRetention)
                ?? PhysicsApplyResult.Failed,
            PierceCollisionResponse => _pierceStrategy?.Apply(host, contact, response.VelocityRetention)
                ?? PhysicsApplyResult.Failed,
            SlideCollisionResponse  => _slideStrategy?.Apply(host, contact, response.VelocityRetention)
                ?? PhysicsApplyResult.Failed,
            IgnoreCollisionResponse => PhysicsApplyResult.Applied,
            _ => PhysicsApplyResult.Failed
        };
    }

    // ─── Response Resolution ────────────────────────

    private BaseCollisionResponse? ResolveResponse(CollisionContact contact)
    {
        // 1. Category match from Identity
        if (contact.Identity != null)
        {
            foreach (var mapping in _categoryResponses)
            {
                if (mapping.Matches(contact.Identity) && mapping.Response != null)
                {
                    return GetActiveResponse(mapping.Response);
                }
            }
        }

        // 2. Normal-based fallback — infer Ground/Wall for colliders without Identity
        if (_useNormalFallback && contact.Identity == null)
        {
            var inferredCategory = InferCategoryFromNormal(contact.Normal);
            if (inferredCategory != null)
            {
                foreach (var mapping in _categoryResponses)
                {
                    if (MappingMatchesCategory(mapping, inferredCategory) && mapping.Response != null)
                    {
                        return GetActiveResponse(mapping.Response);
                    }
                }
            }
        }

        // 3. DefaultResponse
        return _defaultResponse;
    }

    /// <summary>
    /// Follows the fallback chain to return the currently active response.
    /// Supports multi-level fallback chains (A → B → C).
    /// </summary>
    private BaseCollisionResponse GetActiveResponse(BaseCollisionResponse original)
    {
        var current = original;
        while (_activeFallbacks.TryGetValue(current, out var fallback))
        {
            current = fallback;
        }
        return current;
    }

    private Category? InferCategoryFromNormal(Vector3 normal)
    {
        if (_groundCategory != null && normal.Dot(Vector3.Up) > 0.5f)
        {
            return _groundCategory;
        }
        return _wallCategory;
    }

    /// <summary>
    /// Checks if a mapping contains a specific category using CategoryName string comparison,
    /// consistent with Identity.HasCategory.
    /// </summary>
    private static bool MappingMatchesCategory(CategoryResponseMapping mapping, Category category)
    {
        foreach (var cat in mapping.MatchCategories)
        {
            if (cat?.CategoryName == category.CategoryName)
            {
                return true;
            }
        }
        return false;
    }

    // ─── Count Registration ─────────────────────────

    private void RegisterAllCounts()
    {
        foreach (var mapping in _categoryResponses)
        {
            if (mapping.Response is DurableCollisionResponse durable)
            {
                RegisterCounts(durable);
            }
        }
        if (_defaultResponse is DurableCollisionResponse durableDefault)
        {
            RegisterCounts(durableDefault);
        }
    }

    private void RegisterCounts(DurableCollisionResponse response)
    {
        if (_remainingCounts.ContainsKey(response)) { return; }

        // Delegate resolution to the response's definition (handles constant vs attribute-driven)
        int count = response.ResolveMaxCount(GetStatProvider());

        if (count >= 0)
        {
            _remainingCounts[response] = count;
        }
    }

    private void ActivateFallback(BaseCollisionResponse original, BaseCollisionResponse fallback)
    {
        _activeFallbacks[original] = fallback;
        if (fallback is DurableCollisionResponse durableFallback)
        {
            RegisterCounts(durableFallback);
        }
    }

    // ─── Self-Damage Resolution ─────────────────────

    private float ResolveSelfDamage(ICollisionHost host, DurableCollisionResponse response)
    {
        if (response.SelfDamageDefinition == null) { return 0f; }

        float velocity = host.Controller.PreMoveVelocity.Length();
        return response.SelfDamageDefinition.ResolveCollisionDamage(velocity, GetStatProvider());
    }

    /// <summary>
    /// Returns the stat provider for stat-driven count resolution and modifier application.
    /// Uses the test override in unit tests; falls back to the configured provider in production.
    /// </summary>
    private IStatProvider? GetStatProvider()
    {
        if (_testStatProviderOverride != null)
        {
            return _testStatProviderOverride;
        }

        return _statProvider;
    }

    // ─── Pierce Debounce Helpers ─────────────────────

    private bool IsSustainedPierceContact(CollisionContact contact)
    {
        double currentTime = _testTimeOverride ?? Time.GetTicksMsec() / 1000.0;
        if (_lastPierceHitTimes.TryGetValue(contact.Collider, out double lastHit))
        {
            if (currentTime - lastHit < DEBOUNCE_TIME_SECONDS)
            {
                // Extend the debounce window (sliding window)
                _lastPierceHitTimes[contact.Collider] = currentTime;
                return true;
            }
        }
        return false;
    }

    private void RecordPierceHit(CollisionContact contact)
    {
        double currentTime = _testTimeOverride ?? Time.GetTicksMsec() / 1000.0;
        _lastPierceHitTimes[contact.Collider] = currentTime;
    }

    // ─── Post-Collision Stat Modifiers ───────────────

    private void ApplyBounceStatModifiers(DurableCollisionResponse response)
    {
        if (response is not BounceCollisionResponse)
        {
            return;
        }

        if (_gravityScaleAttribute == null && _bounceSpeedAttribute == null)
        {
            return;
        }

        var statProvider = GetStatProvider();
        if (statProvider == null)
        {
            return;
        }

        // One-time gravity modifier — makes entity arc more after first bounce
        if (!_gravityModified && _gravityScaleAttribute != null)
        {
            var gravMod = new FloatAttributeModifier(
                _postBounceGravityMultiplier,
                CanonicalStageRules.FloatMultiply, priority: 0);
            statProvider.TryAddModifier(_gravityScaleAttribute, gravMod, this, out _);
            _gravityModified = true;
        }

        // Per-bounce speed modifier — compounding velocity retention via stat system
        // Prevents movement system from overriding the reduced velocity
        if (_bounceSpeedAttribute != null && response.VelocityRetention < 1.0f)
        {
            var speedMod = new FloatAttributeModifier(
                response.VelocityRetention,
                CanonicalStageRules.FloatMultiply, priority: 0);
            statProvider.TryAddModifier(_bounceSpeedAttribute, speedMod, this, out _);
        }
    }

    private void ApplyPierceStatModifiers(DurableCollisionResponse response)
    {
        if (response is not PierceCollisionResponse)
        {
            return;
        }

        if (response.VelocityRetention >= 1.0f || _pierceStrategy?.SpeedAttribute == null)
        {
            return;
        }

        var statProvider = GetStatProvider();
        if (statProvider == null)
        {
            return;
        }

        // Per-pierce speed modifier — compounding velocity retention
        var speedMod = new FloatAttributeModifier(
            response.VelocityRetention,
            CanonicalStageRules.FloatMultiply, priority: 0);
        statProvider.TryAddModifier(
            _pierceStrategy.SpeedAttribute, speedMod, this, out _);
    }
}
