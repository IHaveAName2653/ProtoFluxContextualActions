using Elements.Core;
using HarmonyLib;
using ProtoFlux.Runtimes.Execution.Nodes.Operators;
using ProtoFluxContextualActions.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly HashSet<Type> ArithmeticBinaryOperatorGroup = [
    typeof(ValueAdd<>),
    typeof(ValueSub<>),
    typeof(ValueMul<>),
    typeof(ValueDiv<>),
    typeof(ValueMod<>),
    typeof(ValueSquare<>),
  ];

  internal static IEnumerable<MenuItem> ArithmeticBinaryOperatorGroupItems(ContextualContext context)
  {
    var psuedoGenerics = context.World.GetPsuedoGenericTypesForWorld();
    var SqrtGroup = psuedoGenerics.SqrtGroup().ToDictionary();
    bool isSqrt = false;
    IEnumerable<Type>? sqrtTypes = null;
    if (SqrtGroup.TryGetValue(context.NodeType, out var typeArgument))
    {
      isSqrt = true;
      sqrtTypes = typeArgument;
    }
    if (TypeUtils.TryGetGenericTypeDefinition(context.NodeType, out var genericType) || isSqrt)
    {
      bool inGroup = false;
      if (genericType != null) inGroup = ArithmeticBinaryOperatorGroup.Contains(genericType);

      if (inGroup || isSqrt)
      {
        Type? opType = null;
        if (inGroup) opType = context.NodeType.GenericTypeArguments[0];
        if (isSqrt && sqrtTypes != null)
        {
          opType = sqrtTypes.First();
        }
        if (opType == null) yield break;

        var coder = Traverse.Create(typeof(Coder<>).MakeGenericType(opType));

        if (coder.Property<bool>("SupportsAddSub").Value)
        {
          yield return new MenuItem(typeof(ValueAdd<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Math Operations");
          yield return new MenuItem(typeof(ValueSub<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Math Operations");
        }

        if (coder.Property<bool>("SupportsMul").Value)
        {
          yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Math Operations");
        }

        if (coder.Property<bool>("SupportsDiv").Value)
        {
          yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Math Operations");
        }

        if (coder.Property<bool>("SupportsMod").Value)
        {
          yield return new MenuItem(typeof(ValueMod<>).MakeGenericType(opType), connectionTransferType: ConnectionTransferType.ByIndexLossy, group: "Math Operations");
        }

        if (coder.Property<bool>("SupportsMul").Value)
        {
          yield return new MenuItem(
              node: typeof(ValueSquare<>).MakeGenericType(opType),
              connectionTransferType: ConnectionTransferType.ByIndexLossy,
              group: "Math Operations"
          );
        }

        var matchingNodes = SqrtGroup
        .Where(a => a.Value.FirstOrDefault() == opType)
        .Select(a => a.Key);

        foreach (var match in matchingNodes)
        {
          yield return new MenuItem(
              node: match,
              connectionTransferType: ConnectionTransferType.ByIndexLossy,
              group: "Math Operations"
          );
        }
      }
    }
  }

}