namespace Jmodot.Implementation.Input;

using System.Collections.Generic;
using Core.Input;
using Godot.Collections;
using Input = Godot.Input;

[GlobalClass]
public partial class PlayerIntentSource : Node, IIntentSource
{
    private readonly System.Collections.Generic.Dictionary<InputAction, IntentData> _currentIntents = new();

    // This is now a unified, data-driven system. No more special-casing movement.
    [ExportGroup("Action Bindings")]
    [Export]
    private Array<ActionBinding> _actionBindings = new();

    [Export] private Array<VectorActionBinding> _vectorBindings = new();
    [Export] public bool IsActive { get; set; } = true;

    public override void _Ready()
    {
        base._Ready();
        UpdateIntentState();
    }

    /// <summary>
    ///     Returns a snapshot of the intents captured during the last _Process frame.
    ///     This method is safe to call from _PhysicsProcess.
    /// </summary>
    public IReadOnlyDictionary<InputAction, IntentData> GetIntents()
    {
        return _currentIntents;
    }

    /// <summary>
    ///     Input polling happens in _Process to ensure no inputs are missed.
    /// </summary>
    public override void _Process(double delta)
    {
        if (!this.IsActive)
        {
            if (this._currentIntents.Count > 0)
            {
                this._currentIntents.Clear();
            }

            return;
        }

        UpdateIntentState();
    }

    private void UpdateIntentState()
    {
        this._currentIntents.Clear();

        // Process all boolean/pressed/released actions
        foreach (var binding in this._actionBindings)
        {
            if (binding?.Action == null || string.IsNullOrEmpty(binding.GodotActionName))
            {
                continue;
            }

            var result = false;
            switch (binding.PollType)
            {
                case InputActionPollType.JustPressed:
                    result = Input.IsActionJustPressed(binding.GodotActionName);
                    break;
                case InputActionPollType.Pressed:
                    result = Input.IsActionPressed(binding.GodotActionName);
                    break;
                case InputActionPollType.JustReleased:
                    result = Input.IsActionJustReleased(binding.GodotActionName);
                    break;
            }

            //if (result)
            //{
            //    this._currentIntents[binding.Action] = new IntentData(true);
            //}
            _currentIntents[binding.Action] = new IntentData(result);
        }

        // Process all vector actions
        foreach (var binding in this._vectorBindings)
        {
            if (binding?.Action == null)
            {
                continue;
            }

            var moveVector = Input.GetVector(binding.Left, binding.Right, binding.Up, binding.Down);
            //if (moveVector.LengthSquared() > 0)
            //{
            //    this._currentIntents[binding.Action] = new IntentData(moveVector);
            //}
            _currentIntents[binding.Action] = new IntentData(moveVector);
        }
        //GD.Print("INTENTS!!");
        //foreach (var intent in _currentIntents)
        //{
        //    GD.Print($"intent: {intent.Key.ActionName} - value: {intent.Value.GetValue()}");
        //}
    }
}
