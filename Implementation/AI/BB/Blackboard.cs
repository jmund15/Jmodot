namespace Jmodot.Implementation.AI.BB;

using System.Diagnostics;
using Core.AI.BB;
using Godot.Collections;

/// <summary>
///     A centralized data container for sharing state between various game systems, particularly for AI agents.
///     It uses StringName keys for performant access and supports a hierarchical structure for data inheritance and
///     propagation.
/// </summary>
[GlobalClass]
[Tool]
public partial class Blackboard : Node, IBlackboard
{
    protected IBlackboard? ParentBB { get; set; }
    protected Dictionary<StringName, Variant> BBData { get; set; } = new();

    /// <summary>
    ///     Establishes a parent blackboard, allowing this blackboard to fall back to the parent
    ///     for data retrieval if a key is not found locally.
    /// </summary>
    /// <param name="parentBB">The IBlackboard instance to use as a parent.</param>
    public void SetParent(IBlackboard? parentBB)
    {
        this.ParentBB = parentBB;
    }

    /// <summary>
    ///     Retrieves a reference type value from the blackboard.
    ///     Performs a recursive lookup through parent blackboards if the key is not found locally.
    /// </summary>
    /// <typeparam name="T">The expected object type, must be a class.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <returns>The requested object, or null if it does not exist or has a mismatched type.</returns>
    public T? GetVar<T>(StringName key) where T : class
    {
        if (this.BBData.TryGetValue(key, out var val))
        {
            if (val.Obj is T tVal) return tVal;

            GD.PrintErr(
                $"BB \"{this.Name}\" ERROR || Requested data for key \"{key}\" exists, but the requested type \"{typeof(T).Name}\" does not match the stored type \"{val.Obj?.GetType().Name}\"!");
            return null;
        }

        if (this.ParentBB != null) return this.ParentBB.GetVar<T>(key);

        GD.PrintErr(
            $"BB \"{this.Name}\" ERROR || Requested data with key \"{key}\" does not exist in this or any parent blackboard!");
        return null;
    }

    /// <summary>
    ///     Sets a reference type value in the blackboard. The new value must match the type of an existing value if present.
    ///     The value is also propagated down to any child blackboard.
    /// </summary>
    /// <typeparam name="T">The type of the object, must be a class.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <param name="val">The object to store.</param>
    /// <returns>Error.Ok on success, Error.InvalidData if a type mismatch occurs.</returns>
    public Error SetVar<T>(StringName key, T val) where T : class
    {
        if (this.BBData.TryGetValue(key, out var oldVal))
            // Enforce strict type safety. Do not allow changing the type of an existing variable.
            if (oldVal.Obj != null && val != null && oldVal.Obj.GetType() != val.GetType())
            {
                var lastFrame = new StackFrame(1);
                GD.PrintErr(
                    $"Var attempted set from {lastFrame.GetMethod().DeclaringType.Name}'s {lastFrame.GetMethod().Name}");
                GD.PrintErr($"BB \"{this.Name}\" ERROR || Inconsistent data type for key \"{key}\"!"
                            + $"\nOriginal data was of type \"{oldVal.Obj.GetType().Name}\", but attempted set data was of type \"{typeof(T).Name}\".");
                return Error.InvalidData;
            }

        this.BBData[key] = Variant.From(val);

        return Error.Ok;
    }

    /// <summary>
    ///     Retrieves a value type (struct) from the blackboard.
    ///     Performs a recursive lookup through parent blackboards if the key is not found locally.
    /// </summary>
    /// <typeparam name="T">The expected struct type.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <returns>A nullable struct. Returns the value if found, or null if it does not exist or has a mismatched type.</returns>
    public T? GetPrimVar<[MustBeVariant] T>(StringName key) where T : struct
    {
        if (this.BBData.TryGetValue(key, out var val))
        {
            if (val.Obj is T tVal) return tVal;

            GD.PrintErr(
                $"BB \"{this.Name}\" ERROR || Requested data for key \"{key}\" exists, but the requested type \"{typeof(T).Name}\" does not match the stored type \"{val.Obj?.GetType().Name}\"!");
            return null;
        }

        if (this.ParentBB != null) return this.ParentBB.GetPrimVar<T>(key);

        GD.PrintErr(
            $"BB \"{this.Name}\" ERROR || Requested primitive data with key \"{key}\" does not exist in this or any parent blackboard!");
        return null;
    }

    /// <summary>
    ///     Sets a value type (struct) in the blackboard. The new value must match the type of an existing value if present.
    ///     The value is also propagated down to any child blackboard.
    /// </summary>
    /// <typeparam name="T">The type of the struct.</typeparam>
    /// <param name="key">The StringName identifier for the data.</param>
    /// <param name="val">The struct to store.</param>
    /// <returns>Error.Ok on success, Error.InvalidData if a type mismatch occurs.</returns>
    public Error SetPrimVar<[MustBeVariant] T>(StringName key, T val) where T : struct
    {
        if (this.BBData.TryGetValue(key, out var oldVal))
            // Enforce strict type safety. Do not allow changing the type of an existing variable.
            if (oldVal.Obj is not T)
            {
                var lastFrame = new StackFrame(1);
                GD.PrintErr(
                    $"Var attempted set from {lastFrame.GetMethod().DeclaringType.Name}'s {lastFrame.GetMethod().Name}");
                GD.PrintErr($"BB \"{this.Name}\" ERROR || Inconsistent data type for key \"{key}\"!"
                            + $"\nOriginal data was of type \"{oldVal.Obj?.GetType().Name}\", but attempted set data was of type \"{typeof(T).Name}\".");
                return Error.InvalidData;
            }

        this.BBData[key] = Variant.From(val);

        return Error.Ok;
    }
}
