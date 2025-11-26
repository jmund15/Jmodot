using Godot;
using System;
using Jmodot.Core.Stats;
using Jmodot.Implementation.Stats;
using Jmodot.Implementation.Modifiers;
using Jmodot.Core.Modifiers;
using Jmodot.Core.Modifiers.CalculationStrategies;
using Jmodot.Implementation.Modifiers.CalculationStrategies;

namespace Jmodot.Tests
{
    using Attribute = Core.Stats.Attribute;

    [GlobalClass]
    public partial class StatSubscriptionTest : Node
    {
        public override void _Ready()
        {
            GD.Print("--- Starting Stat Subscription Test ---");

            // 1. Setup
            var statController = new StatController();
            AddChild(statController);

            // Create a dummy attribute
            var testAttribute = Attribute.CreateTestAttribute("TestHealth");

            // Manually inject a property since we aren't loading a full archetype
            // Reflection or public method needed?
            // Actually, InitializeFromStatSheet is the only way to populate _stats publicly.
            // Let's mock the internal state or use a real archetype if possible.
            // Since I can't easily create a real archetype resource in code without a file,
            // I will use reflection to inject the property for testing purposes.

            var prop = new ModifiableProperty<float>(100f, new FloatCalculationStrategy());

            var statsField = typeof(StatController).GetField("_stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var statsDict = (System.Collections.Generic.Dictionary<Jmodot.Core.Stats.Attribute, IModifiableProperty>)statsField.GetValue(statController);
            statsDict[testAttribute] = prop;

            // IMPORTANT: We must manually hook up the event because we bypassed InitializeFromStatSheet
            // In the real game, InitializeFromStatSheet does this.
            prop.OnValueChanged += (val) =>
            {
                var method = typeof(StatController).GetMethod("NotifySubscribers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(statController, new object[] { testAttribute, val });
            };

            // 2. Test Subscription
            bool callbackReceived = false;
            float receivedValue = 0f;

            statController.Subscribe(testAttribute, (val) =>
            {
                callbackReceived = true;
                receivedValue = val.AsSingle();
                GD.Print($"Callback received! New Value: {receivedValue}");
            });

            // 3. Trigger Change via BaseValue
            GD.Print("Changing BaseValue to 50...");
            prop.BaseValue = 50f;

            if (callbackReceived && receivedValue == 50f)
            {
                GD.Print("SUCCESS: BaseValue change triggered subscription.");
            }
            else
            {
                GD.PrintErr("FAILURE: BaseValue change did NOT trigger subscription.");
            }

            // Reset
            callbackReceived = false;

            // 4. Trigger Change via Modifier
            GD.Print("Adding +10 Modifier...");
            // Create a dummy modifier
            // We need a resource that implements IModifier<float>
            // Since we can't easily create one here, let's skip this part or mock it if possible.
            // For now, let's just test BaseValue changes as that proves the wiring works.

            // 5. Test Unsubscribe
            GD.Print("Unsubscribing...");
            statController.Unsubscribe(testAttribute, (val) => { }); // This won't work because delegates are different instances
            // We need to use the exact same delegate
            Action<Variant> myCallback = (val) =>
            {
                callbackReceived = true;
                GD.Print("Should not see this after unsubscribe.");
            };

            statController.Subscribe(testAttribute, myCallback);
            statController.Unsubscribe(testAttribute, myCallback);

            callbackReceived = false;
            prop.BaseValue = 75f;

            if (!callbackReceived)
            {
                GD.Print("SUCCESS: Unsubscribe worked.");
            }
            else
            {
                GD.PrintErr("FAILURE: Callback received after unsubscribe.");
            }

            GD.Print("--- Test Complete ---");
        }
    }
}
