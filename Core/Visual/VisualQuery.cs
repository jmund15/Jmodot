namespace Jmodot.Core.Visual;

using System.Collections.Generic;
using System.Linq;
using Godot;
using Jmodot.Core.Visual.Animation.Sprite;

/// <summary>
/// Filter primitive for selecting <see cref="VisualNodeHandle"/>s from an
/// <see cref="IVisualNodeProvider"/>. Combine via <see cref="And"/>/<see cref="Or"/>;
/// construct via the static factories.
/// </summary>
public abstract record VisualQuery
{
    /// <summary>True if this query matches the given handle.</summary>
    public abstract bool Matches(VisualNodeHandle handle);

    public static VisualQuery All { get; } = new AllQuery();
    public static VisualQuery VisibleOnly { get; } = new VisibleOnlyQuery();

    public static VisualQuery Slot(SlotKey key) => new SlotQuery(key);
    public static VisualQuery Part(StringName partId) => new PartQuery(partId);
    public static VisualQuery Tagged(StringName tag) => new TaggedQuery(tag);
    public static VisualQuery Tagged(params StringName[] tags) => new TaggedAnyQuery(tags);
    public static VisualQuery AllExceptSlot(SlotKey key) => new AllExceptSlotQuery(key);
    public static VisualQuery AllExceptTag(StringName tag) => new AllExceptTagQuery(tag);
    public static VisualQuery Handles(params VisualNodeHandle[] handles) => new HandlesQuery(handles);
    public static VisualQuery Handles(IEnumerable<VisualNodeHandle> handles) => new HandlesQuery(System.Linq.Enumerable.ToArray(handles));

    public VisualQuery And(VisualQuery other) => new AndQuery(this, other);
    public VisualQuery Or(VisualQuery other) => new OrQuery(this, other);
}

internal sealed record AllQuery : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => true;
}

internal sealed record VisibleOnlyQuery : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => handle.IsVisible;
}

internal sealed record SlotQuery(SlotKey Key) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => Key.Equals(handle.SlotId);
}

internal sealed record PartQuery(StringName PartId) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => handle.PartId == PartId;
}

internal sealed record TaggedQuery(StringName Tag) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => handle.Tags.Contains(Tag);
}

internal sealed record TaggedAnyQuery(StringName[] Tags) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle)
    {
        foreach (var t in Tags)
        {
            if (handle.Tags.Contains(t)) { return true; }
        }
        return false;
    }
}

internal sealed record AllExceptSlotQuery(SlotKey Key) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => !Key.Equals(handle.SlotId);
}

internal sealed record AllExceptTagQuery(StringName Tag) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => !handle.Tags.Contains(Tag);
}

internal sealed record HandlesQuery(VisualNodeHandle[] Items) : VisualQuery
{
    private readonly HashSet<VisualNodeHandle> _set = new(Items);
    public override bool Matches(VisualNodeHandle handle) => _set.Contains(handle);
}

internal sealed record AndQuery(VisualQuery Left, VisualQuery Right) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => Left.Matches(handle) && Right.Matches(handle);
}

internal sealed record OrQuery(VisualQuery Left, VisualQuery Right) : VisualQuery
{
    public override bool Matches(VisualNodeHandle handle) => Left.Matches(handle) || Right.Matches(handle);
}
