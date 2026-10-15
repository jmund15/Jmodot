#ifndef SPRITE_MATERIAL_GDSHADERINC
#define SPRITE_MATERIAL_GDSHADERINC
#ifndef SPRITE_EFFECT_CEILING
#define SPRITE_EFFECT_CEILING 1.0
#endif
#ifndef SPRITE_TEXTURE_FILTER
#define SPRITE_TEXTURE_FILTER filter_linear_mipmap
#endif
#ifndef SPRITE_LIGHT_WRAP
#define SPRITE_LIGHT_WRAP 1.0
#endif
#ifndef SPRITE_LIGHT_ROUNDNESS
#define SPRITE_LIGHT_ROUNDNESS 0.8
#endif

uniform sampler2D sprite_texture : source_color, SPRITE_TEXTURE_FILTER;
uniform vec4 sprite_frame_rect = vec4(0.0, 0.0, 1.0, 1.0);
uniform float alpha_scissor_threshold = 0.5;
uniform float alpha_hash_scale = 1.0;
instance uniform vec4 effect_emission : source_color = vec4(0.0);

#define SPRITE_VERTEX \
    if (!OUTPUT_IS_SRGB) { \
        COLOR.rgb = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), \
            COLOR.rgb / 12.92, lessThan(COLOR.rgb, vec3(0.04045))); \
    } \
    SPRITE_VERTEX_BILLBOARD \
    SPRITE_VERTEX_FIXED_SIZE

#if defined(SPRITE_BILLBOARD)
#define SPRITE_VERTEX_BILLBOARD \
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(MAIN_CAM_INV_VIEW_MATRIX[0], MAIN_CAM_INV_VIEW_MATRIX[1], MAIN_CAM_INV_VIEW_MATRIX[2], MODEL_MATRIX[3]); \
    MODELVIEW_MATRIX *= mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), \
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), \
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0)); \
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
#elif defined(SPRITE_BILLBOARD_FIXED_Y)
#define SPRITE_VERTEX_BILLBOARD \
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4( \
        vec4(normalize(cross(vec3(0.0, 1.0, 0.0), MAIN_CAM_INV_VIEW_MATRIX[2].xyz)), 0.0), \
        vec4(0.0, 1.0, 0.0, 0.0), \
        vec4(normalize(cross(MAIN_CAM_INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0), MODEL_MATRIX[3]); \
    MODELVIEW_MATRIX *= mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), \
        vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), \
        vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0)); \
    MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
#else
#define SPRITE_VERTEX_BILLBOARD
#endif

#ifdef SPRITE_FIXED_SIZE
#define SPRITE_VERTEX_FIXED_SIZE \
    float sprite_scale = PROJECTION_MATRIX[3][3] != 0.0 \
        ? abs(1.0 / PROJECTION_MATRIX[1][1]) \
        : (IS_MULTIVIEW ? length(MODELVIEW_MATRIX[3].xyz) : -MODELVIEW_MATRIX[3].z); \
    MODELVIEW_MATRIX[0] *= sprite_scale; \
    MODELVIEW_MATRIX[1] *= sprite_scale; \
    MODELVIEW_MATRIX[2] *= sprite_scale;
#else
#define SPRITE_VERTEX_FIXED_SIZE
#endif

#ifdef SPRITE_ALPHA_BLEND
#define SPRITE_ALPHA_BODY \
    ALPHA = sprite_texel.a * COLOR.a; \
    ALBEDO *= ALPHA;
#define SPRITE_SPECULAR SPECULAR = 0.5 * sqrt(ALPHA);
#elif defined(SPRITE_ALPHA_SCISSOR)
#define SPRITE_ALPHA_BODY \
    ALPHA = sprite_texel.a * COLOR.a; \
    ALPHA_SCISSOR_THRESHOLD = alpha_scissor_threshold;
#define SPRITE_SPECULAR SPECULAR = 0.5;
#elif defined(SPRITE_ALPHA_HASH)
#define SPRITE_ALPHA_BODY \
    ALPHA = sprite_texel.a * COLOR.a; \
    ALPHA_HASH_SCALE = alpha_hash_scale;
#define SPRITE_SPECULAR SPECULAR = 0.5;
#else
#define SPRITE_ALPHA_BODY
#define SPRITE_SPECULAR SPECULAR = 0.5;
#endif

#ifdef SPRITE_UNSHADED
#define SPRITE_EMIT(value) ALBEDO += (value);
#else
#define SPRITE_EMIT(value) EMISSION += (value);
#endif

// A flat sprite stands in for a body with volume. Lit through its camera-facing quad, a light level with it and to
// one side meets the quad edge-on and leaves it black beside a floor that light brightens. It is lit instead as a
// rounded surface facing halfway between world up and the camera, with wrapped falloff, so it takes light wherever
// the floor beside it does and on the side the light comes from. The bulge follows the screen, not the UV, so a
// flipped frame stays lit on the correct side. A normal map perturbs the basis through NORMAL_MAP; its body sets
// SPRITE_LIGHT_ROUNDNESS to 0, since the map carries the shape.
#ifdef SPRITE_UNSHADED
#define SPRITE_LIGHT_BASIS
#else
#define SPRITE_LIGHT_BASIS \
    NORMAL = normalize(normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz) + vec3(0.0, 0.0, 1.0)); \
    TANGENT = normalize(vec3(1.0, 0.0, 0.0) - NORMAL * NORMAL.x); \
    BINORMAL = cross(NORMAL, TANGENT); \
    vec2 sprite_bulge = (UV - (sprite_frame_rect.xy + sprite_frame_rect.zw) * 0.5) \
        / max((sprite_frame_rect.zw - sprite_frame_rect.xy) * 0.5, vec2(1e-4)); \
    sprite_bulge.x *= dFdx(UV.x) < 0.0 ? -1.0 : 1.0; \
    NORMAL = normalize(NORMAL + (TANGENT * sprite_bulge.x - BINORMAL * sprite_bulge.y) * SPRITE_LIGHT_ROUNDNESS);

void light() {
    float facing = max((dot(NORMAL, LIGHT) + SPRITE_LIGHT_WRAP) / (1.0 + SPRITE_LIGHT_WRAP), 0.0);
    DIFFUSE_LIGHT += facing * ATTENUATION * LIGHT_COLOR / PI;
}
#endif

#define SPRITE_FRAGMENT \
    SPRITE_LIGHT_BASIS \
    vec4 sprite_texel = texture(sprite_texture, UV); \
    ALBEDO = sprite_texel.rgb * COLOR.rgb; \
    SPRITE_ALPHA_BODY \
    SPRITE_SPECULAR

#define SPRITE_EFFECT_CHANNELS \
    float effect_weight = clamp(effect_emission.a, 0.0, 1.0); \
    vec3 effect_target = clamp(effect_emission.rgb, vec3(0.0), vec3(SPRITE_EFFECT_CEILING)); \
    ALBEDO *= 1.0 - effect_weight; \
    SPRITE_EFFECT_SPECULAR \
    SPRITE_EMIT(effect_target * effect_weight * sprite_texel.a)

#ifdef SPRITE_UNSHADED
#define SPRITE_EFFECT_SPECULAR
#else
#define SPRITE_EFFECT_SPECULAR SPECULAR *= sqrt(1.0 - effect_weight);
#endif
#endif
