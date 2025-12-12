using Elements.Core;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform;
using ProtoFluxContextualActions.Extensions;
using ProtoFluxContextualActions.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly BiDictionary<Type, Type> SetGlobalLocalEquivilents = new()
  {
    {typeof(SetGlobalPosition), typeof(SetLocalPosition)},
    {typeof(SetGlobalPositionRotation), typeof(SetLocalPositionRotation)},
    {typeof(SetGlobalRotation), typeof(SetLocalRotation)},
    {typeof(SetGlobalScale), typeof(SetLocalScale)},
    {typeof(SetGlobalTransform), typeof(SetLocalTransform)},
  };

  static readonly BiDictionary<Type, Type> GetGlobalLocalEquivilents = new()
  {
    {typeof(GlobalTransform), typeof(LocalTransform)}
  };

  static readonly Dictionary<Type, Type> GetterToSetterConversion = new()
  {
    {typeof(GlobalTransform), typeof(SetGlobalTransform)},
    {typeof(LocalTransform), typeof(SetLocalTransform)}
  };
  static readonly Dictionary<Type, Type> SetterToGetterConversion = new()
  {
    {typeof(SetGlobalPosition), typeof(GlobalTransform)},
    {typeof(SetGlobalPositionRotation), typeof(GlobalTransform)},
    {typeof(SetGlobalRotation), typeof(GlobalTransform)},
    {typeof(SetGlobalScale), typeof(GlobalTransform)},
    {typeof(SetGlobalTransform), typeof(GlobalTransform)},

    {typeof(SetLocalPosition), typeof(LocalTransform)},
    {typeof(SetLocalPositionRotation), typeof(LocalTransform)},
    {typeof(SetLocalRotation), typeof(LocalTransform)},
    {typeof(SetLocalScale), typeof(LocalTransform)},
    {typeof(SetLocalTransform), typeof(LocalTransform)},
  };

  internal static IEnumerable<MenuItem> GlobalLocalEquivilentSwapGroups(Type nodeType)
  {
    if (TryGetSwap(SetGlobalLocalEquivilents, nodeType, out Type match))
    {
      if (SetterToGetterConversion.TryGetValue(nodeType, out Type? getNode))
      {
        yield return new(getNode, group: "Transform Getter");
      }
      yield return new(match, group: "Transform");
    }
    if (TryGetSwap(GetGlobalLocalEquivilents, nodeType, out match))
    {
      if (GetterToSetterConversion.TryGetValue(nodeType, out Type? setNode))
      {
        yield return new(setNode, group: "Transform Setter");
      }
      yield return new(match, connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Transform");
    }
  }
}