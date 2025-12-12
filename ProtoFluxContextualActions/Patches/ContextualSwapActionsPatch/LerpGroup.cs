using Elements.Core;
using ProtoFlux.Runtimes.Execution.Nodes.Math;
using HarmonyLib;
using ProtoFluxContextualActions.Utils;
using System;
using System.Collections.Generic;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly HashSet<Type> LerpGroup = [
    typeof(ValueSmoothLerp<>),
    typeof(ValueConstantLerp<>),
    typeof(ValueLerp<>),
    typeof(ValueLerpUnclamped<>),
    typeof(ValueMultiLerp<>),
    typeof(ValueInverseLerp<>),
  ];

  internal static IEnumerable<MenuItem> LerpGroupItems(ContextualContext context)
  {
    if (TypeUtils.TryGetGenericTypeDefinition(context.NodeType, out var genericType) && LerpGroup.Contains(genericType))
    {
      var opCount = context.NodeType.GenericTypeArguments.Length;
      var opType = context.NodeType.GenericTypeArguments[opCount - 1];
      var coder = Traverse.Create(typeof(Coder<>).MakeGenericType(opType));

      // in theory, this check shouldn't be needed
      // in practice, https://github.com/Yellow-Dog-Man/Resonite-Issues/issues/3319
      if (coder.Property<bool>("SupportsSmoothLerp").Value)
      {
        yield return new(typeof(ValueSmoothLerp<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
      }
      if (coder.Property<bool>("SupportsConstantLerp").Value)
      {
        yield return new(typeof(ValueConstantLerp<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
      }
      if (coder.Property<bool>("SupportsLerp").Value)
      {
        yield return new(typeof(ValueLerp<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
        yield return new(typeof(ValueLerpUnclamped<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
        yield return new(typeof(ValueMultiLerp<>).MakeGenericType(opType), name: "MultiLerp", connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
      }
      if (coder.Property<bool>("SupportsInverseLerp").Value)
      {
        yield return new(typeof(ValueInverseLerp<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Lerp");
      }
    }
  }
}