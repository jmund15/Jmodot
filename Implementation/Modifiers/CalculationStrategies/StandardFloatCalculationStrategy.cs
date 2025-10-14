// namespace Jmodot.Implementation.Modifiers.CalculationStrategies;
//
// using System.Collections.Generic;
// using System.Linq;
// using Core.Modifiers;
// using Core.Modifiers.CalculationStrategies;
//
// public class StandardFloatCalculationStrategy : ICalculationStrategy<float>
// {
//     public float Calculate(float baseValue, List<IModifier<float>> modifiers)
//     {
//         // We must cast to the more specific IFloatModifier to access the Stage property.
//         var floatModifiers = modifiers.Cast<IFloatModifier>().ToList();
//
//         // Step 0: Conflict Resolution (already done in ModifiableProperty)
//         // The list passed in is assumed to be the final, filtered list.
//
//         var currentValue = baseValue;
//
//         // Stage 1: BaseAdd
//         var baseAddMods = floatModifiers.Where(m => m.Stage == CalculationStage.BaseAdd);
//         foreach (var mod in baseAddMods)
//         {
//             currentValue = mod.Modify(currentValue);
//         }
//
//         // Stage 2: PercentAdd
//         var percentAddMods = floatModifiers.Where(m => m.Stage == CalculationStage.PercentAdd);
//         if (percentAddMods.Any())
//         {
//             var totalPercentBonus = 0f;
//             foreach (var mod in
//                      percentAddMods)
//             {
//                 totalPercentBonus += mod.Modify(0); // Modify returns the value for this stage
//             }
//
//             currentValue *= 1.0f + totalPercentBonus;
//         }
//
//         // Stage 3: FinalMultiply
//         var finalMultMods = floatModifiers.Where(m => m.Stage == CalculationStage.FinalMultiply);
//         foreach (var mod in finalMultMods)
//         {
//             currentValue = mod.Modify(currentValue);
//         }
//
//         return currentValue;
//     }
// }
