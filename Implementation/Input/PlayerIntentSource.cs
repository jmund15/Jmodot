namespace Jmodot.Implementation.Input;

using System.Collections.Generic;
using Core.Input;
using GCol = Godot.Collections;
using Input = Godot.Input;

[GlobalClass]
public partial class PlayerIntentSource : IntentSourceNode
{
    // Dictionary for _Process consumers (e.g., UI). Rebuilt every frame.
    private Dictionary<InputAction, IntentData> _processIntents = new();

    // Dictionary for _PhysicsProcess consumers. Acts as a buffer.
    private Dictionary<InputAction, IntentData> _physicsIntents = new();

    // --- The Physics "Staging" Buffer ---
    // _Process writes into this buffer. _PhysicsProcess copies it to _physicsIntents.
    private readonly Dictionary<InputAction, IntentData> _physicsBuffer = new();

    // This is now a unified, data-driven system. No more special-casing movement.
    [ExportGroup("Action Bindings")]
    [Export]
    private GCol.Array<ActionBinding> _actionBindings = new();

    [Export] private GCol.Array<VectorActionBinding> _vectorBindings = new();
    [Export] public bool IsActive { get; set; } = true;

    public override void _Ready()
    {
        base._Ready();
        ProcessFrameUpdateIntentStates();
        PhysicsFramePublishBuffer();
    }

    /// <summary>
    ///     Returns a snapshot of the intents captured during the last _Process frame.
    ///     This method is safe to call from _Process.
    /// </summary>
    /// <remarks>Ideal for UI.</remarks>
    public override IReadOnlyDictionary<InputAction, IntentData> GetProcessIntents()
    {
        return _processIntents;
    }
    /// <summary>
    ///     Returns a snapshot of the intents captured during the last _PhysicsProcess frame.
    ///     This method is safe to call from _PhysicsProcess.
    /// </summary>
    /// <remarks>Ideal for physics inputs.</remarks>
    public override IReadOnlyDictionary<InputAction, IntentData> GetPhysicsIntents()
    {
        return _physicsIntents;
    }

    /// <summary>
    ///     Input polling happens in _Process to ensure no inputs are missed.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!IsActive)
        {
            if (_processIntents.Count > 0)
            {
                _processIntents.Clear();
            }
            return;
        }

        ProcessFrameUpdateIntentStates();
    }

    /// <summary>
    /// On the physics tick, this acts as the synchronization point.
    /// It "publishes" the collected inputs from the buffer to the public physics dictionary.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        PhysicsFramePublishBuffer();
    }

    // call with either process or physics
    private void ProcessFrameUpdateIntentStates()
    {
        // Always clear the process-specific dictionary for a fresh snapshot.
        _processIntents.Clear();

        // --- Poll and update the buffers ---
        // Process all boolean/pressed/released actions
        foreach (var binding in _actionBindings)
        {
            if (binding?.Action == null) continue;

            bool isJustPressed = Input.IsActionJustPressed(binding.GodotActionName);
            bool isPressed = Input.IsActionPressed(binding.GodotActionName);
            bool isJustReleased = Input.IsActionJustReleased(binding.GodotActionName);

            // 1. Populate the _processIntents dictionary for immediate UI feedback.
            var processResult = binding.PollType switch {
                InputActionPollType.JustPressed => isJustPressed,
                InputActionPollType.Pressed => isPressed,
                InputActionPollType.JustReleased => isJustReleased,
                _ => false
            };
            if (processResult) _processIntents[binding.Action] = new IntentData(processResult);

            // 2. Update the physics buffer.
            // For continuous states, always set the latest value.
            if (binding.PollType == InputActionPollType.Pressed)
            {
                _physicsBuffer[binding.Action] = new IntentData(isPressed);
            }
            // For one-shot events, only add them to the buffer if they occurred.
            else if (isJustPressed && binding.PollType == InputActionPollType.JustPressed)
            {
                _physicsBuffer[binding.Action] = new IntentData(true);
            }
            else if (isJustReleased && binding.PollType == InputActionPollType.JustReleased)
            {
                _physicsBuffer[binding.Action] = new IntentData(true);
            }
        }

        // Process all vector actions
        foreach (var binding in _vectorBindings)
        {
            if (binding?.Action == null)
            {
                continue;
            }
            var moveVector = Input.GetVector(binding.Left, binding.Right, binding.Up, binding.Down);
            // Vectors are continuous states, so update both dictionaries/buffers.
            _processIntents[binding.Action] = new IntentData(moveVector);
            _physicsBuffer[binding.Action] = new IntentData(moveVector);
        }
    }

    private void PhysicsFramePublishBuffer()
    {
        // Step 1: Publish the buffer.
        // Overwrite the public dictionary with a copy of everything the buffer has collected.
        _physicsIntents.Clear();
        foreach (var intent in _physicsBuffer)
        {
            _physicsIntents[intent.Key] = intent.Value;
        }

        // Step 2: Clear transient events from the buffer.
        // We do this so "JustPressed" actions don't persist into the next physics frame.
        // Continuous actions like "Pressed" or vectors are left alone, as they will be
        // overwritten by the next _Process call anyway.
        List<InputAction> actionsToClear = new();
        foreach (var binding in _actionBindings)
        {
            if (binding.PollType == InputActionPollType.JustPressed || binding.PollType == InputActionPollType.JustReleased)
            {
                if (_physicsBuffer.ContainsKey(binding.Action))
                {
                    actionsToClear.Add(binding.Action);
                }
            }
        }

        foreach (var action in actionsToClear)
        {
            _physicsBuffer.Remove(action);
        }
    }
}
