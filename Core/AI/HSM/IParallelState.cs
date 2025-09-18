namespace Jmodot.Core.AI.HSM;

/// <summary>
/// A marker interface used to identify states that can run in parallel with a primary state.
/// A CompoundState will automatically register any child state implementing this interface
/// as a parallel state.
/// </summary>
public interface IParallelState { }
