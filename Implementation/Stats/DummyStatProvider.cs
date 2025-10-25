namespace Jmodot.Implementation.Stats;

using Core.Modifiers;
using Core.Stats;
using Core.Stats.Mechanics;
using Modifiers;

public class DummyStatProvider : IStatProvider
{
    public event System.Action<Attribute, Variant>? OnStatChanged;
    public ModifiableProperty<T> GetStat<T>(Attribute attribute)
    {
        throw new System.NotImplementedException();
    }

    public T GetStatValue<T>(Attribute attribute, T defaultValue = default(T))
    {
        throw new System.NotImplementedException();
    }

    public T GetMechanicData<T>(MechanicType mechanicType) where T : MechanicData
    {
        throw new System.NotImplementedException();
    }

    public bool TryAddModifier(Attribute attribute, Resource modifierResource, object owner, out ModifierHandle? handle)
    {
        throw new System.NotImplementedException();
    }

    public void RemoveModifier(ModifierHandle handle)
    {
        throw new System.NotImplementedException();
    }

    public void RemoveAllModifiersFromSource(object owner)
    {
        throw new System.NotImplementedException();
    }

    public void AddActiveContext(StatContext context)
    {
        throw new System.NotImplementedException();
    }

    public void RemoveActiveContext(StatContext context)
    {
        throw new System.NotImplementedException();
    }
}
