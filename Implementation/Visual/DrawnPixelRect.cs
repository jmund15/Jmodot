namespace Jmodot.Implementation.Visual;

using System.Collections.Generic;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// The rectangle of a texture region that holds visible pixels. A texture's image is read and
/// decompressed once, on first touch, and every later rect of that texture is answered from that one
/// image, so callers may ask every frame.
/// </summary>
/// <remarks>
/// Results are cached per texture instance for the life of the process. Decompressing a VRAM-compressed
/// texture costs tens of milliseconds, which is why the answer is never recomputed.
/// </remarks>
public static class DrawnPixelRect
{
    private sealed class TextureEntry
    {
        public Image? Image;
        public readonly Dictionary<Rect2I, Rect2> Rects = new();
    }

    private static readonly Dictionary<ulong, TextureEntry> Entries = new();

    /// <summary>
    /// The sub-rect of <paramref name="sourceRect"/> that holds non-transparent pixels, in source pixels.
    /// Returns <paramref name="sourceRect"/> itself when that region is fully transparent (never an empty
    /// rect), and when the texture's image cannot be read, which also warns once per texture.
    /// </summary>
    /// <param name="texture">The texture whose pixels are sampled, not an <see cref="AtlasTexture"/> wrapper.</param>
    /// <param name="sourceRect">The region of the texture to examine, in texture pixels.</param>
    public static Rect2 Of(Texture2D texture, Rect2I sourceRect)
    {
        var fallback = new Rect2(sourceRect.Position, sourceRect.Size);
        if (texture == null || !GodotObject.IsInstanceValid(texture)) { return fallback; }

        ulong id = texture.GetInstanceId();
        if (!Entries.TryGetValue(id, out TextureEntry? entry))
        {
            entry = new TextureEntry { Image = ReadImage(texture) };
            Entries[id] = entry;
        }

        if (entry.Image == null) { return fallback; }
        if (entry.Rects.TryGetValue(sourceRect, out Rect2 cached)) { return cached; }

        Rect2 drawn = Trim(entry.Image, sourceRect, fallback);
        entry.Rects[sourceRect] = drawn;
        return drawn;
    }

    private static Image? ReadImage(Texture2D texture)
    {
#if TOOLS
        ImageReadCountForTest++;
#endif
        Image? image = texture.GetImage();
        if (image == null)
        {
            JmoLogger.Warning(texture, "DrawnPixelRect: the texture's image is unreadable, so its art is measured by its whole frame.");
            return null;
        }

        if (image.IsCompressed()) { image.Decompress(); }
        return image;
    }

    private static Rect2 Trim(Image image, Rect2I sourceRect, Rect2 fallback)
    {
        Rect2I inside = sourceRect.Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        if (inside.Size.X <= 0 || inside.Size.Y <= 0) { return fallback; }

        Rect2I used = image.GetRegion(inside).GetUsedRect();
        if (used.Size.X <= 0 || used.Size.Y <= 0) { return fallback; }

        return new Rect2(inside.Position + used.Position, used.Size);
    }

#if TOOLS
    #region Test Helpers

    internal static int ImageReadCountForTest { get; private set; }

    #endregion
#endif
}
