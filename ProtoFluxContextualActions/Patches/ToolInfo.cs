using Elements.Core;
using Elements.Quantity;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using ProtoFlux.Core;
using ProtoFluxContextualActions.Attributes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoFluxContextualActions.Patches;

[HarmonyPatch]
internal static class ToolInfo
{
  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxTool), "CleanupDraggedWire")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void CleanupDraggedWire(ProtoFluxTool instance)
  {
    var traverse = Traverse.Create(instance);
    traverse.Field<SyncRef<Slot>>("_currentTempWire").Value.Target?.Destroy();
    traverse.Field<SyncRef<ProtoFluxElementProxy>>("_currentProxy").Value.Target = null;
    traverse.Field<SyncRef<Slot>>("_currentTempWire").Value.Target = null;
  }

  //[HarmonyReversePatch]
  //[HarmonyPatch(typeof(ProtoFluxHelper), "GetNodeForType")]
  //[MethodImpl(MethodImplOptions.NoInlining)]
  internal static Type GetNodeForType(Type type, List<NodeTypeRecord> list)
  {
    foreach (NodeTypeRecord item in list)
    {
      if (!item.CanHandleType(type))
      {
        continue;
      }

      if (item.baseType.IsGenericTypeDefinition)
      {
        try
        {
          Type type2 = item.InstanceType(type);
          if (type2.IsValidGenericType(validForInstantiation: true))
          {
            return type2;
          }
        }
        catch
        {
        }

        continue;
      }

      return item.baseType;
    }

    return null;
  }

  //[HarmonyReversePatch]
  //[HarmonyPatch(typeof(Tool), "GetHit")]
  //[MethodImpl(MethodImplOptions.NoInlining)]
  internal static RaycastHit? GetHit(Tool instance)
  {
    InteractionLaser interactionLaser = instance.ActiveHandler?.Laser;
    if (interactionLaser != null)
    {
      if (interactionLaser.CurrentHit == null)
      {
        return null;
      }

      return interactionLaser.LastHit;
    }

    return instance.Physics.RaycastOne(instance.Tip, instance.TipForward);
  }

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxNode), "AssociateInstance")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void AssociateInstance(ProtoFluxNode instance, ProtoFluxNodeGroup group, INode node) => typeof(ProtoFluxNode).GetMethod("AssociateInstance", System.Reflection.BindingFlags.NonPublic)?.Invoke(instance, [group, node]);

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxNode), "ClearGroupAndInstance")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void ClearGroupAndInstance(this ProtoFluxNode instance) => typeof(ProtoFluxNode).GetMethod("ClearGroupAndInstance", System.Reflection.BindingFlags.NonPublic)?.Invoke(instance, []);
}
