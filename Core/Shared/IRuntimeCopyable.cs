namespace Jmodot.Core.Shared;

public interface IRuntimeCopyable<T>
{
    void CopyStateFrom(T original);
}
