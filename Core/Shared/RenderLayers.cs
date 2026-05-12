namespace Jmodot.Core.Shared;

/// <summary>
/// Project-wide visual-layer (rendering) convention for Godot 4 <c>VisualInstance3D</c>
/// and <c>Decal</c> nodes. Mirrors Godot's 20-bit visual-layer system as named flags so
/// scene authors can opt surfaces into receiving projected effects (shadows, aiming
/// previews, etc.) without memorizing raw bitmask values.
/// </summary>
/// <remarks>
/// <para>
/// Conventions:
/// <list type="bullet">
/// <item><b>Entities</b> (player, enemies, projectiles, items) — leave <c>layers</c>
/// at the default <see cref="Default"/>. Do NOT add them to <see cref="DecalReceiver"/>;
/// doing so re-introduces the "shadow-on-owner" bug, since decals would project onto
/// the entity's own mesh.</item>
/// <item><b>Ground / terrain / floor / wall</b> meshes that should receive shadows or
/// aiming-preview decals — set <c>layers = Default | DecalReceiver</c> (value <c>3</c>).</item>
/// <item><b>Decal</b> nodes that should project ONLY onto opted-in surfaces (drop
/// shadows, placement previews) — set <c>cull_mask = DecalReceiver</c> (value <c>2</c>).</item>
/// </list>
/// </para>
/// <para>
/// Failure mode is loud-by-design: a new floor that forgets to opt in shows no shadow
/// (visible at first playtest), rather than silently re-introducing shadow-on-self by
/// requiring entities to opt out.
/// </para>
/// <para>
/// NOTE: These constants are for the <c>VisualInstance3D.layers</c> / <c>Decal.cull_mask</c>
/// (rendering) layer system. They are distinct from physics collision layers
/// (<c>CollisionObject3D.collision_layer</c>) and must not be passed to
/// <c>PhysicsRayQueryParameters3D.collision_mask</c>.
/// </para>
/// </remarks>
public static class RenderLayers
{
    /// <summary>Bit 0 — default rendering layer. Every <c>VisualInstance3D</c> starts here.</summary>
    public const uint Default = 1u << 0;

    /// <summary>Bit 1 — surfaces that should receive projected decals (ground, terrain, walls
    /// designated as shadow / aiming-preview receivers). Set on the mesh's <c>layers</c>
    /// alongside <see cref="Default"/>; set on a <c>Decal</c>'s <c>cull_mask</c> by itself.</summary>
    public const uint DecalReceiver = 1u << 1;
}
