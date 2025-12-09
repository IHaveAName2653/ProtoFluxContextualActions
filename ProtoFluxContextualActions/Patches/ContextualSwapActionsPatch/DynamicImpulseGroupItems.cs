using FrooxEngine.ProtoFlux;
using Microsoft.VisualBasic;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ProtoFluxContextualActions.Utils;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  public static readonly FrozenSet<Type> DynamicImpulseGroup = [
    typeof(DynamicImpulseTrigger),
    typeof(DynamicImpulseReceiver),
  ];

  public static readonly FrozenSet<Type> AsyncDynamicImpulseGroup = [
    typeof(AsyncDynamicImpulseTrigger),
    typeof(AsyncDynamicImpulseReceiver),
  ];

  public static readonly FrozenSet<Type> DynamicImpulseValueGroup = [
    typeof(DynamicImpulseTriggerWithValue<>),
    typeof(DynamicImpulseReceiverWithValue<>),
  ];

  public static readonly FrozenSet<Type> AsyncDynamicImpulseValueGroup = [
    typeof(AsyncDynamicImpulseTriggerWithValue<>),
    typeof(AsyncDynamicImpulseReceiverWithValue<>),
  ];

  public static readonly FrozenSet<Type> DynamicImpulseObjectGroup = [
    typeof(DynamicImpulseTriggerWithObject<>),
    typeof(DynamicImpulseReceiverWithObject<>),
  ];

  public static readonly FrozenSet<Type> AsyncDynamicImpulseObjectGroup = [
    typeof(AsyncDynamicImpulseTriggerWithObject<>),
    typeof(AsyncDynamicImpulseReceiverWithObject<>),
  ];

  public static readonly IEnumerable<FrozenSet<Type>> ImpulseGroups = [
    DynamicImpulseGroup,
    AsyncDynamicImpulseGroup
  ];

  public static readonly IEnumerable<FrozenSet<Type>> ImpulseGroupsWithData = [
    DynamicImpulseValueGroup,
    AsyncDynamicImpulseValueGroup,
    DynamicImpulseObjectGroup,
    AsyncDynamicImpulseObjectGroup
  ];

  static readonly IEnumerable<FrozenSet<Type>> AllDynamicImpulseGroup = [
    .. ImpulseGroups,
    .. ImpulseGroupsWithData
  ];


  internal static IEnumerable<MenuItem> DynamicImpulseGroupItems(ContextualContext context)
  {
    if (DynamicImpulseGroup.Any(t => context.NodeType.IsGenericType ? t == context.NodeType.GetGenericTypeDefinition() : t == context.NodeType))
    {
      List<Type> DynTriggerTypes = [
        typeof(DynamicImpulseTrigger),
        typeof(AsyncDynamicImpulseTrigger)
      ];
      List<Type> DynReceiverTypes = [
        typeof(DynamicImpulseReceiver),
        typeof(AsyncDynamicImpulseReceiver)
      ];

      Type? target = null;
      if (context.proxy is ProtoFluxInputProxy)
      {
        ProtoFluxInputProxy inputType = (ProtoFluxInputProxy)(context.proxy);
        Type targetType = inputType.InputType;
        target = targetType;
      }
      if (context.proxy is ProtoFluxOutputProxy)
      {
        ProtoFluxOutputProxy outputType = (ProtoFluxOutputProxy)(context.proxy);
        Type targetType = outputType.OutputType;
        target = targetType;
      }
      if (context.NodeType.IsGenericType && target == null)
      {
        var opCount = context.NodeType.GenericTypeArguments.Length;
        var opType = context.NodeType.GenericTypeArguments[opCount - 1];
        target = opType;
      }

      if (target != null)
      {
        var DynTrigger = GetNodeForType(target, [
          new NodeTypeRecord(typeof(DynamicImpulseTriggerWithValue<>), null, null),
          new NodeTypeRecord(typeof(DynamicImpulseTriggerWithObject<>), null, null),
        ]);
        DynTriggerTypes.Add(DynTrigger);
        var AsyncDynTrigger = GetNodeForType(target, [
          new NodeTypeRecord(typeof(AsyncDynamicImpulseTriggerWithValue<>), null, null),
          new NodeTypeRecord(typeof(AsyncDynamicImpulseTriggerWithObject<>), null, null),
        ]);
        DynTriggerTypes.Add(AsyncDynTrigger);

        var DynReceiver = GetNodeForType(target, [
          new NodeTypeRecord(typeof(DynamicImpulseReceiverWithValue<>), null, null),
          new NodeTypeRecord(typeof(DynamicImpulseReceiverWithObject<>), null, null),
        ]);
        DynTriggerTypes.Add(DynReceiver);

        var AsyncDynReceiver = GetNodeForType(target, [
          new NodeTypeRecord(typeof(AsyncDynamicImpulseReceiverWithValue<>), null, null),
          new NodeTypeRecord(typeof(AsyncDynamicImpulseReceiverWithObject<>), null, null),
        ]);
        DynTriggerTypes.Add(AsyncDynReceiver);

      }

      foreach (Type dynType in DynTriggerTypes)
      {
        yield return new(dynType);
      }
      foreach (Type dynType in DynReceiverTypes)
      {
        yield return new(dynType);
      }
    }
  }
}