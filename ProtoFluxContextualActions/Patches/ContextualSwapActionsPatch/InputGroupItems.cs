using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;
using ProtoFluxContextualActions.Utils;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  internal static IEnumerable<MenuItem> InputGroupItems(ContextualContext context)
  {
    if (TypeUtils.TryGetGenericTypeDefinition(context.NodeType, out var genericType))
    {
      Type valInputType = typeof(ExternalValueInput<,>);
      Type objInputType = typeof(ExternalObjectInput<,>);
      if (genericType != valInputType && genericType != objInputType) yield break;

      MenuItem makeValueType<T>() => new(ProtoFluxHelper.GetInputNode(typeof(T)), name: typeof(T).GetNiceTypeName(), group: "Value Input");
      MenuItem makeObjectType<T>() => new(ProtoFluxHelper.GetInputNode(typeof(T)), name: typeof(T).GetNiceTypeName(), group: "Value Input");

      if (genericType == valInputType)
      {
        yield return makeValueType<bool>();
        yield return makeValueType<int>();
        yield return makeValueType<float>();
        yield return makeValueType<float3>();
        yield return makeObjectType<string>();

        yield return makeObjectType<Slot>();
        yield return makeObjectType<User>();
      }
      if (genericType == objInputType)
      {
        yield return makeObjectType<Slot>();
        yield return makeObjectType<User>();


        yield return makeObjectType<IWorldElement>();

        yield return makeObjectType<string>();
        yield return makeValueType<bool>();
        yield return makeValueType<float>();

      }
    }
  }
}