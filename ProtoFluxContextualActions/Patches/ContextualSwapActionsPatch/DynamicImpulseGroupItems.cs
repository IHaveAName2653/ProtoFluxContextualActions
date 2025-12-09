using Elements.Core;
using FrooxEngine.ProtoFlux;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly HashSet<Type> DynamicImpulseGroup = [
    typeof(DynamicImpulseReceiver),
    typeof(DynamicImpulseTrigger),
    typeof(DynamicImpulseReceiverWithValue<>),
    typeof(DynamicImpulseReceiverWithObject<>),
    typeof(DynamicImpulseTriggerWithValue<>),
    typeof(DynamicImpulseTriggerWithObject<>),

    typeof(AsyncDynamicImpulseReceiver),
    typeof(AsyncDynamicImpulseTrigger),
    typeof(AsyncDynamicImpulseReceiverWithValue<>),
    typeof(AsyncDynamicImpulseReceiverWithObject<>),
    typeof(AsyncDynamicImpulseTriggerWithValue<>),
    typeof(AsyncDynamicImpulseTriggerWithObject<>),
  ];


  internal static IEnumerable<MenuItem> DynamicImpulseGroupItems(ContextualContext context)
  {
    if (DynamicImpulseGroup.Any(t => context.NodeType.IsGenericType ? t == context.NodeType.GetGenericTypeDefinition() : t == context.NodeType))
    {
      List<Type> DynOutputs = [
        typeof(DynamicImpulseTrigger),
        typeof(DynamicImpulseReceiver),
        typeof(AsyncDynamicImpulseTrigger),
        typeof(AsyncDynamicImpulseReceiver)
      ];

      Type? target = null;
      bool hasProxyHeld = false;
      if (context.proxy is ProtoFluxInputProxy)
      {
        ProtoFluxInputProxy inputType = (ProtoFluxInputProxy)(context.proxy);
        Type targetType = inputType.InputType;
        target = targetType;
        hasProxyHeld = true;
      }
      if (context.proxy is ProtoFluxOutputProxy)
      {
        ProtoFluxOutputProxy outputType = (ProtoFluxOutputProxy)(context.proxy);
        Type targetType = outputType.OutputType;
        target = targetType;
        hasProxyHeld = true;
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
        var AsyncDynTrigger = GetNodeForType(target, [
          new NodeTypeRecord(typeof(AsyncDynamicImpulseTriggerWithValue<>), null, null),
          new NodeTypeRecord(typeof(AsyncDynamicImpulseTriggerWithObject<>), null, null),
        ]);

        var DynReceiver = GetNodeForType(target, [
          new NodeTypeRecord(typeof(DynamicImpulseReceiverWithValue<>), null, null),
          new NodeTypeRecord(typeof(DynamicImpulseReceiverWithObject<>), null, null),
        ]);

        var AsyncDynReceiver = GetNodeForType(target, [
          new NodeTypeRecord(typeof(AsyncDynamicImpulseReceiverWithValue<>), null, null),
          new NodeTypeRecord(typeof(AsyncDynamicImpulseReceiverWithObject<>), null, null),
        ]);

        if (hasProxyHeld)
        {
          DynOutputs.Insert(0, AsyncDynReceiver);
          DynOutputs.Insert(0, AsyncDynTrigger);
          DynOutputs.Insert(0, DynReceiver);
          DynOutputs.Insert(0, DynTrigger);
        }
        else
        {
          DynOutputs.Add(DynTrigger);
          DynOutputs.Add(DynReceiver);
          DynOutputs.Add(AsyncDynTrigger);
          DynOutputs.Add(AsyncDynReceiver);
        }
      }

      
      foreach (Type dynType in DynOutputs)
      {
        yield return new(dynType);
      }
    }
  }
}