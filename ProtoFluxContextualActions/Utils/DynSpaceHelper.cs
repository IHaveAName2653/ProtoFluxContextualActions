using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using static FrooxEngine.DynamicVariableSpace;

namespace ProtoFluxContextualActions;

public static class DynSpaceHelper
{
  public static DynamicVariableSpace TryGetSpace(Slot root, string SpaceName, bool createIfNotExist = false)
  {
    DynamicVariableSpace space = root.FindSpace(SpaceName);
    if (space != null) return space;
    if (!createIfNotExist) return null;
    space = root.AttachComponent<DynamicVariableSpace>();
    space.SpaceName.Value = SpaceName;
    return space;
  }

  public static DynamicVariableBase<T>? CreateVariable<T>(DynamicVariableSpace space, string variableName)
  {
    ValueManager<T> manager = space.GetManager<T>(variableName, false);
    if (manager == null || manager.ReadableValueCount == 0)
    {
      DynamicVariableBase<T>? newComponent = null;
      if (typeof(T) == typeof(Type))
      {
        newComponent = (DynamicVariableBase<T>)space.Slot.AttachComponent(typeof(DynamicTypeVariable));
      }
      else
      {
        Type? dynVar = (typeof(T).IsValueType || typeof(T) == typeof(string)) ? typeof(DynamicValueVariable<>) : typeof(DynamicReferenceVariable<>);
        newComponent = (DynamicVariableBase<T>)space.Slot.AttachComponent(dynVar.MakeGenericType(typeof(T)));
      }
      newComponent.VariableName.Value = variableName; //space.SpaceName + "/" + variableName;
      return newComponent;
    }
    else return null;
  }

  public static bool TryWrite<T>(DynamicVariableSpace space, string variableName, T value, bool createIfNotExist = false)
  {
    ValueManager<T> manager = space.GetManager<T>(variableName, false);
    if (manager == null || manager.ReadableValueCount == 0)
    {
      if (!createIfNotExist)
      {
        return false;
      }
      else
      {
        var newVar = CreateVariable<T>(space, space.SpaceName + "/" + variableName);
        if (newVar == null) return false;
        newVar.DynamicValue = value;
        return true;
      }
    }

    if (!manager.IsValidValue(value))
    {
      return false;
    }

    manager.SetValue(value);

    return true;
  }
  public static bool TryRead<T>(DynamicVariableSpace space, string variableName, out T value, bool createIfNotExist = false)
  {
    ValueManager<T> manager = space.GetManager<T>(variableName, false);
    if (manager == null || manager.ReadableValueCount == 0)
    {
      if (!createIfNotExist)
      {
        value = Coder<T>.Default;
        return false;
      }
      else
      {
        var newVar = CreateVariable<T>(space, space.SpaceName + "/" + variableName);
        value = newVar.DynamicValue ?? Coder<T>.Default;
        return false;
      }
    }

    value = manager.Value;
    return true;
  }
  public static bool VariableExists<T>(DynamicVariableSpace space, string variableName)
  {
    ValueManager<T> manager = space.GetManager<T>(variableName, false);
    if (manager == null || manager.ReadableValueCount == 0) return false;
    return true;
  }

  public static bool EnsureVariableExists<T>(DynamicVariableSpace space, string variableName, T valueIfNotExist)
  {
    if (!VariableExists<T>(space, variableName))
    {
      TryWrite(space, variableName, valueIfNotExist, true);
      return false;
    }
    return true;
  }
  public static bool EnsureVariableExists<T>(DynamicVariableSpace space, string variableName)
  {
    if (!VariableExists<T>(space, variableName))
    {
      var coder = Traverse.Create(typeof(Coder<T>));
      TryWrite(space, variableName, coder.Property<T>("Default").Value, true);
      return false;
    }
    return true;
  }

  public static bool TryGetArg<T>(DynamicVariableSpace space, int index, out T value, bool createIfNotExist = false) => TryRead(space, $"Arg{index}", out value, createIfNotExist);

  public static bool TryGetArg<T>(DynamicVariableSpace space, string variableName, out T value, bool createIfNotExist = false) => TryRead(space, variableName, out value, createIfNotExist);

  public static bool TryGetArgOrName<T>(DynamicVariableSpace space, int index, string name, out T value, bool createIfNotExist = true)
  {
    bool hasOverride = TryRead(space, "UseIndex", out bool overrideIndex);
    bool userWantsIndex = ProtoFluxContextualActions.ReadIndexFirst();
    if ((hasOverride && overrideIndex) || (!hasOverride && userWantsIndex))
    {
      if (TryGetArg(space, index, out value, createIfNotExist)) return true;
      if (createIfNotExist) return true;
      if (TryGetArg(space, name, out value, createIfNotExist)) return true;
      if (createIfNotExist) return true;
    }
    else
    {
      if (TryGetArg(space, name, out value, createIfNotExist)) return true;
      if (createIfNotExist) return true;
      if (TryGetArg(space, index, out value, createIfNotExist)) return true;
      if (createIfNotExist) return true;
    }
    return false;
  }

  public static Dictionary<DynamicVariableSpace, Dictionary<Type, IDynamicVariable>> ReturnVariables = [];
  public static bool ReturnFromFunc<T>(DynamicVariableSpace space, int returnIndex, string returnName, T value)
  {
    bool hadSpace = false;
    string varName = $"Return_{returnName}";
    if (ProtoFluxContextualActions.ReadIndexFirst()) varName = $"Return_{returnIndex}";
    if (ReturnVariables.TryGetValue(space, out Dictionary<Type, IDynamicVariable>? thisSpaceVars))
    {
      hadSpace = true;
      foreach (var kv in thisSpaceVars)
      {
        if (kv.Key == typeof(T))
        {
          var thisvar = (DynamicVariableBase<T>)kv.Value;
          thisvar.VariableName.Value = varName;
          return TryWrite(space, varName, value, true);
        }
      }
    }
    DynamicVariableBase<T>? newVar = CreateVariable<T>(space, varName);
    if (newVar != null)
    {
      if (!hadSpace) ReturnVariables.Add(space, []);
      ReturnVariables[space].Add(typeof(T), newVar);
    }
    return TryWrite(space, varName, value, true);
  }
}
