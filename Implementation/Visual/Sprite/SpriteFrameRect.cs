namespace Jmodot.Implementation.Visual.Sprite;

using Godot;

public static class SpriteFrameRect
{
    /// <summary>Returns the sampled texture, current frame UV bounds and drawn frame size; returns an empty size without a texture.</summary>
    public static (Texture2D? Sampled, Vector4 Rect, Vector2 DrawnSize) Of(SpriteBase3D sprite)
    {
        Texture2D? frame;
        Rect2 source;
        int columns = 1;
        int rows = 1;
        int index = 0;
        Vector2 drawnSize;
        switch (sprite)
        {
            case Sprite3D still:
                frame = still.Texture;
                columns = still.Hframes;
                rows = still.Vframes;
                index = still.Frame;
                source = still.RegionEnabled ? still.RegionRect : new Rect2(Vector2.Zero, frame?.GetSize() ?? Vector2.Zero);
                drawnSize = source.Size;
                break;
            case AnimatedSprite3D animated:
                frame = animated.SpriteFrames?.GetFrameTexture(animated.Animation, animated.Frame);
                source = new Rect2(Vector2.Zero, frame?.GetSize() ?? Vector2.Zero);
                drawnSize = source.Size;
                break;
            default:
                return (null, new Vector4(0f, 0f, 1f, 1f), Vector2.Zero);
        }

        if (frame == null) { return (null, new Vector4(0f, 0f, 1f, 1f), Vector2.Zero); }
        Texture2D sampled = frame;
        if (frame is AtlasTexture atlas && atlas.Atlas != null)
        {
            sampled = atlas.Atlas;
            Vector2 regionSize = new Vector2(
                atlas.Region.Size.X > 0f ? atlas.Region.Size.X : sampled.GetWidth(),
                atlas.Region.Size.Y > 0f ? atlas.Region.Size.Y : sampled.GetHeight());
            bool wholeFrame = sprite is not Sprite3D still || !still.RegionEnabled;
            source = new Rect2(source.Position + atlas.Region.Position, wholeFrame ? regionSize : source.Size);
        }
        drawnSize /= new Vector2(Mathf.Max(1, columns), Mathf.Max(1, rows));
        return (sampled, Compute(sampled.GetSize(), source, columns, rows, index), drawnSize);
    }

    public static Vector4 Compute(Vector2 sampledSize, Rect2 sourceRect, int hframes, int vframes, int frame)
    {
        if (!(sampledSize.X > 0f) || !(sampledSize.Y > 0f)) { return new Vector4(0f, 0f, 1f, 1f); }
        int columns = Mathf.Max(1, hframes);
        int rows = Mathf.Max(1, vframes);
        Vector2 frameSize = sourceRect.Size / new Vector2(columns, rows);
        Vector2 start = sourceRect.Position + new Vector2(frame % columns, frame / columns) * frameSize;
        Vector2 end = start + frameSize;
        return new Vector4(start.X / sampledSize.X, start.Y / sampledSize.Y,
            end.X / sampledSize.X, end.Y / sampledSize.Y);
    }
}
