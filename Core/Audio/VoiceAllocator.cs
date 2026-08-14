namespace Jmodot.Core.Audio;

using Jmodot.Implementation.Audio;

/// <summary>
/// Opaque handle to an allocated voice slot. <see cref="None"/> is the drop sentinel a
/// request gets when no free voice exists and nothing is stealable.
/// </summary>
public readonly struct VoiceHandle
{
    /// <summary>The drop sentinel; never maps to a live slot.</summary>
    public static readonly VoiceHandle None = new(-1);

    /// <summary>Index of the backing voice slot, or -1 for <see cref="None"/>.</summary>
    public readonly int SlotIndex;

    public VoiceHandle(int slotIndex) => SlotIndex = slotIndex;

    /// <summary>Whether this handle is the drop sentinel.</summary>
    public bool IsNone => SlotIndex < 0;
}

/// <summary>
/// Pure-core voice-slot allocator for the audio director's voice pool. Allocatable states
/// are modeled as data (opaque handles + allocator-tracked insertion order) with no Node or
/// <c>AudioStream</c> reference, so the steal policy is Logic-Domain testable. The thin node
/// adapter lives in <c>AudioDirectorBase</c> and maps a handle back to the live voice node.
/// </summary>
public sealed class VoiceAllocator
{
    private readonly VoiceSlot[] _slots;
    private long _nextSequence;

    public VoiceAllocator(int voiceCount)
    {
        var count = System.Math.Max(0, voiceCount);
        _slots = new VoiceSlot[count];
        for (int i = 0; i < count; i++)
        {
            _slots[i] = new VoiceSlot();
        }
    }

    /// <summary>
    /// Reserve a voice for <paramref name="priority"/>. Returns a free slot when one exists;
    /// otherwise steals the oldest voice in the lowest tier strictly below
    /// <paramref name="priority"/> (a request never evicts an equal or higher tier). When
    /// neither exists, returns <see cref="VoiceHandle.None"/> and the request is dropped.
    /// </summary>
    public VoiceHandle Allocate(SoundPriority priority)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (!_slots[i].Occupied)
            {
                return Occupy(i, priority);
            }
        }

        int stealIndex = FindStealCandidate(priority);
        return stealIndex < 0 ? VoiceHandle.None : Occupy(stealIndex, priority);
    }

    /// <summary>Returns a voice to the free list. No-op for <see cref="VoiceHandle.None"/>.</summary>
    public void Release(VoiceHandle handle)
    {
        if (handle.IsNone || handle.SlotIndex < 0 || handle.SlotIndex >= _slots.Length)
        {
            return;
        }
        _slots[handle.SlotIndex].Occupied = false;
    }

    private VoiceHandle Occupy(int index, SoundPriority priority)
    {
        var slot = _slots[index];
        slot.Occupied = true;
        slot.Priority = priority;
        slot.Sequence = _nextSequence++;
        return new VoiceHandle(index);
    }

    private int FindStealCandidate(SoundPriority incoming)
    {
        int bestIndex = -1;
        long bestSequence = long.MaxValue;
        int bestTier = int.MaxValue;
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (!slot.Occupied || (int)slot.Priority >= (int)incoming)
            {
                continue;
            }
            // Stealable tier: strictly below incoming. Prefer the lowest tier; within that
            // tier, the oldest (smallest insertion sequence).
            if ((int)slot.Priority < bestTier
                || ((int)slot.Priority == bestTier && slot.Sequence < bestSequence))
            {
                bestTier = (int)slot.Priority;
                bestSequence = slot.Sequence;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private sealed class VoiceSlot
    {
        public bool Occupied;
        public SoundPriority Priority;
        public long Sequence;
    }
}
