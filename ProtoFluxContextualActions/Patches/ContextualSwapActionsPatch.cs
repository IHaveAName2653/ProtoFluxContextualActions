using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Undo;
using HarmonyLib;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFluxContextualActions.Attributes;
using ProtoFluxContextualActions.Extensions;
using ProtoFluxContextualActions.Utils;
using ProtoFluxContextualActions.Utils.ProtoFlux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ProtoFluxContextualActions.Patches;

// [HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
[HarmonyPatchCategory("ProtoFluxTool Contextual Swap Actions"), TweakCategory("Adds 'Contextual Swapping Actions' to the ProtoFlux Tool. Double pressing secondary pointing at a node with protoflux tool will open a context menu of actions to swap the node for another node.", defaultValue: true)] // unstable, disable by default
internal static partial class ContextualSwapActionsPatch
{
  // TODO: This can be replaced in the future with flags or a combination of the three automatically.
  //       progress has already been made.
  internal enum ConnectionTransferType
  {
    /// <summary>
    /// Transfers the connections by name, connections that are not found and are not of the same type will be lost.
    /// </summary>
    ByNameLossy,
    /// <summary>
    /// Uses names too :)
    /// Transfers the connections by a manually made set of mappings. Unmapped connections will be lost.
    /// </summary>
    ByMappingsLossy,
    /// <summary>
    /// Uses names too :)
    /// Attempts to match inputs of the same type 
    /// </summary>
    ByIndexLossy
  }

  internal readonly struct MenuItem(Type node, string? name = null, ConnectionTransferType? connectionTransferType = ConnectionTransferType.ByNameLossy, string? group = null) : IPageItems
  {
    internal readonly Type node = node;

    internal readonly string? name = name;

    internal readonly ConnectionTransferType? connectionTransferType = connectionTransferType;

    internal readonly string DisplayName => name ?? NodeMetadataHelper.GetMetadata(node).Name ?? node.GetNiceTypeName();

    internal readonly string? group = group;

    public string? GetDisplayName() => DisplayName;

    public string? GetGroup() => group ?? "";

    public Type? GetNodeType() => node;
  }

  internal record ContextualContext(Type NodeType, World World, ProtoFluxElementProxy? proxy);

  internal static bool GetSwapActions(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy)
  {
    var hit = ToolInfo.GetHit(tool);
    if (hit is { Collider.Slot: var hitSlot })
    {
      var hitNode = hitSlot.GetComponentInParents<ProtoFluxNode>();
      if (hitNode != null)
      {
        CreateMenu(tool, hitNode, elementProxy);
        // skip rest
        return false;
      }
    }

    return true;
  }

  private static void CreateMenu(ProtoFluxTool tool, ProtoFluxNode hitNode, ProtoFluxElementProxy proxy)
  {
    var items = GetMenuItems(tool, hitNode, proxy).Where(m => m.node != hitNode.NodeType).ToList();

    var query = new NodeQueryAcceleration(hitNode.NodeInstance.Runtime.Group);

    if (items.Count > 0)
    {
      Pager<MenuItem> swapPager = new();

      swapPager.InitPagedItems(items, colorX.White, proxy, (fluxTool, _, Item) =>
      {
        try
        {
          SwapHitForNode(tool, hitNode, Item);
        }
        finally
        {
          // if there's somehow an error I do not want evil dangling references that world crash silently.
          if (hitNode != null && !hitNode.IsRemoved)
          {
            hitNode.UndoableDestroy();
          }
        }
      }, () => RenderContextMenu(tool, swapPager));

      RenderContextMenu(tool, swapPager);
    }
  }

  internal static void RenderContextMenu(ProtoFluxTool tool, Pager<MenuItem> pagerManager)
  {
    tool.StartTask(async () =>
    {
      var menu = await ContextHelper.CreateContext(tool);

      pagerManager.CreateGroups(tool, menu, colorX.White);
    });
  }

  private static void SwapHitForNode(ProtoFluxTool __instance, ProtoFluxNode hitNode, MenuItem menuItem)
  {
    var undoBatch = __instance.World.BeginUndoBatch($"Swap {hitNode.Name} to {menuItem.DisplayName}");

    var ensureVisualMethodInfo = AccessTools.Method(typeof(ProtoFluxNodeGroup), "EnsureVisualOnRestore", [typeof(Worker)]);
    var ensureVisualDelegate = AccessTools.MethodDelegate<Action<Worker>>(ensureVisualMethodInfo);

    var runtime = hitNode.NodeInstance.Runtime;
    var oldNode = hitNode.NodeInstance;
    var binding = ProtoFluxHelper.GetBindingForNode(menuItem.node);
    var query = new NodeQueryAcceleration(oldNode.Runtime.Group);
    var executionRuntime = Traverse.Create(hitNode.Group).Field<ExecutionRuntime<FrooxEngineContext>>("executionRuntime").Value;

    {
      var newNodeInstance = runtime.AddNode(menuItem.node);
      var tryByIndex = menuItem.connectionTransferType == ConnectionTransferType.ByIndexLossy;
      var results = SwapHelper.TransferElements(oldNode, newNodeInstance, query, executionRuntime, tryByIndex, overload: true);
      var nodeMap = hitNode.Group.Nodes.ToDictionary(a => a.NodeInstance, a => a);
      var swappedNodes = results.Where(r => r.overload?.OverloadedAnyNodes == true).SelectMany(r => r.overload?.SwappedNodes).Append(new(oldNode, newNodeInstance)).ToList();

      foreach (var (fromNode, intoNode) in swappedNodes)
      {
        var intoType = intoNode.GetType();
        var swappedNode = (ProtoFluxNode)nodeMap[fromNode].Slot.AttachComponent(ProtoFluxHelper.GetBindingForNode(intoType));
        nodeMap[intoNode] = swappedNode;
        ToolInfo.AssociateInstance(swappedNode, nodeMap[fromNode].Group, intoNode);
      }

      foreach (var (_, intoNode) in swappedNodes)
      {
        intoNode.MapElements(nodeMap[intoNode], nodeMap, undoable: true);
      }

      foreach (var (fromNode, _) in swappedNodes)
      {
        var oldFromNode = nodeMap[fromNode];
        var oldVisualSlot = oldFromNode.GetVisualSlot();
        oldVisualSlot?.Destroy();
        oldVisualSlot?.Parent.GetComponent<Grabbable>()?.Destroy();
        oldFromNode.ClearGroupAndInstance();
        oldFromNode.UndoableDestroy(oldVisualSlot != null ? ensureVisualDelegate : null);
        runtime.RemoveNode(fromNode);
      }

      foreach (var (_, intoNode) in swappedNodes)
      {
        nodeMap[intoNode].EnsureVisual();
      }

      var newNode = nodeMap[newNodeInstance];
      var dynamicLists = newNode.NodeInputLists
        .Concat(newNode.NodeOutputLists)
        .Concat(newNode.NodeImpulseLists)
        .Concat(newNode.NodeOperationLists);

      foreach (var list in dynamicLists) list.EnsureElementCount(2);

      newNode.EnsureVisual();

      foreach (var (_, intoNode) in swappedNodes)
      {
        var node = nodeMap[intoNode];
        node.CreateSpawnUndoPoint(node.HasActiveVisual() ? ensureVisualDelegate : null);
      }
    }

    __instance.World.EndUndoBatch();
  }

  private static void AddMenuItem(ProtoFluxTool __instance, ContextMenu menu, colorX color, MenuItem item, Action setup)
  {
    var nodeMetadata = NodeMetadataHelper.GetMetadata(item.node);
    var label = (LocaleString)item.DisplayName;
    var menuItem = menu.AddItem(in label, (Uri?)null, color);
    menuItem.Button.LocalPressed += (button, data) =>
    {
      setup();
      __instance.LocalUser.CloseContextMenu(__instance);
    };
  }

  internal static IEnumerable<MenuItem> GetMenuItems(ProtoFluxTool __instance, ProtoFluxNode nodeComponent, ProtoFluxElementProxy elementProxy)
  {
    var node = nodeComponent.NodeInstance;
    var nodeType = node.GetType();
    var context = new ContextualContext(nodeType, __instance.World, elementProxy);

    IEnumerable<MenuItem> menuItems = [
      .. UserRootSwapGroups(nodeType),
      .. GlobalLocalEquivilentSwapGroups(nodeType),
      .. GetDirectionGroupItems(context),
      .. ForLoopGroupItems(context),
      .. EasingOfSameKindFloatItems(context),
      .. EasingOfSameKindDoubleItems(context),
      .. TimespanInstanceGroupItems(context),
      .. SetSlotTranformGlobalOperationGroupItems(context),
      .. SetSlotTranformLocalOperationGroupItems(context),
      .. UserInfoGroupItems(context),
      .. DeltaTimeGroupItems(context),
      .. UserBoolCheckGroupItems(context),
      .. PlayOneShotGroupItems(context),
      .. ScreenPointGroupItems(context),
      .. MousePositionGroupItems(context),
      .. FindSlotGroupItems(context),
      .. SlotMetaGroupItems(context),
      .. UserRootSlotGroupItems(context),
      .. UserRootPositionGroupItems(context),
      .. UserRootRotationGroupItems(context),
      .. SetUserRootPositionGroupItems(context),
      .. SetUserRootRotationGroupItems(context),
      .. UserRootHeadRotationGroupItems(context),
      .. SetUserRootHeadRotationGroupItems(context),
      .. BinaryOperationsGroupItems(context),
      .. BinaryOperationsMultiGroupItems(context),
      .. BinaryOperationsMultiSwapMapItems(context),
      .. NumericLogGroupItems(context),
      .. ApproximatelyGroupItems(context),
      .. AverageGroupItems(context),
      .. VariableStoreNodesGroupItems(context),
      .. ValueRelayGroupItems(context),
      .. ObjectRelayGroupItems(context),
      .. DeltaTimeOperationGroupItems(context),
      .. EnumShiftGroupItems(context),
      .. NullCoalesceGroupItems(context),
      .. MinMaxGroupItems(context),
      .. MinMaxMultiGroupItems(context),
      .. ArithmeticBinaryOperatorGroupItems(context),
      .. ArithmeticMultiOperatorGroupItems(context),
      .. ArithmeticRepeatGroupItems(context),
      .. ArithmeticNegateGroupItems(context),
      .. ArithmeticOneGroupItems(context),
      .. EnumToNumberGroupItems(context),
      .. NumberToEnumGroupItems(context),
      .. MultiInputMappingGroupItems(context),
      .. ApproximatelyNodesGroupItems(context),
      .. GrabbableValuePropertyGroupItems(context),
      .. SinCosSwapGroup(context),
      .. SampleSpatialVariableGroupItems(context),
      .. KeyStateGroupItems(context),
      .. FireOnBoolGroupItems(context),
      .. WriteGroupItems(context),
      .. LerpGroupItems(context),
      .. DynamicImpulseGroupItems(context),
      .. IsNullGroupItemsGroupItems(context),
      .. BinaryComparisonOperatorGroupItems(context),
      .. BooleanVectorToBoolOperationsGroupItems(context),
      .. ShiftRotationOperationsGroupItems(context),
      .. SlotChildGroupItems(context),
      .. GetSlotActiveGroupItems(context),
      .. RepeatGroupItems(context),
      .. InputGroupItems(context),
    ];

    foreach (var menuItem in menuItems)
    {
      yield return menuItem;
    }
  }

  #region Utils
  internal static string FormatMultiName(Type match) =>
    $"{NodeMetadataHelper.GetMetadata(match).Name} (Multi)";

  internal static IEnumerable<MenuItem> MatchNonGenericTypes(ICollection<Type> types, Type type, string? group = null)
  {
    if (types.Contains(type))
    {
      foreach (var match in types)
      {
        yield return new MenuItem(match, group: group);
      }
    }
  }

  internal static IEnumerable<MenuItem> MatchGenericTypes(ISet<Type> types, Type type, string? group = null)
  {
    if (TypeUtils.TryGetGenericTypeDefinition(type, out var genericType) && types.Contains(genericType))
    {
      foreach (var match in types)
      {
        yield return new MenuItem(match.MakeGenericType(type.GenericTypeArguments), group: group);
      }
    }
  }

  internal static bool TryGetSwap(BiDictionary<Type, Type> swaps, Type nodeType, out Type match) =>
    swaps.TryGetSecond(nodeType, out match) || swaps.TryGetFirst(nodeType, out match);

  #endregion
}