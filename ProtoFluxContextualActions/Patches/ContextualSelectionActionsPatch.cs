using Elements.Core;
using Elements.Quantity;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.CoreNodes;
using FrooxEngine.Undo;
using HarmonyLib;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ProtoFlux.Runtimes.Execution.Nodes.Casts;
using ProtoFlux.Runtimes.Execution.Nodes.Enums;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Assets;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Audio;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar.BodyNodes;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Elements;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Keyboard;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Mouse;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tools;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Physics;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.References;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Rendering;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.LocalScreen;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.Roots;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Utility;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Worlds;
using ProtoFlux.Runtimes.Execution.Nodes.Math;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Bounds;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Constants;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Quantity;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Quaternions;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Random;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Rects;
using ProtoFlux.Runtimes.Execution.Nodes.Math.SphericalHarmonics;
using ProtoFlux.Runtimes.Execution.Nodes.Operators;
using ProtoFlux.Runtimes.Execution.Nodes.ParsingFormatting;
using ProtoFlux.Runtimes.Execution.Nodes.Strings;
using ProtoFlux.Runtimes.Execution.Nodes.Strings.Characters;
using ProtoFlux.Runtimes.Execution.Nodes.TimeAndDate;
using ProtoFlux.Runtimes.Execution.Nodes.Utility;
using ProtoFlux.Runtimes.Execution.Nodes.Utility.Uris;
using ProtoFluxContextualActions.Attributes;
using ProtoFluxContextualActions.Extensions;
using ProtoFluxContextualActions.Tagging;
using ProtoFluxContextualActions.Utils;
using Renderite.Shared;
using SharpPipe;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static ProtoFluxContextualActions.Utils.PsuedoGenericUtils;

namespace ProtoFluxContextualActions.Patches;

[HarmonyPatchCategory("ProtoFluxTool Contextual Actions"), TweakCategory("Adds 'Contextual Actions' to the ProtoFlux Tool. Pressing secondary while holding a protoflux tool will open a context menu of actions based on what wire you're dragging instead of always spawning an input/display node. Pressing secondary again will spawn out an input/display node like normal.")]
// [HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.OnSecondaryPress))]
internal static class ContextualSelectionActionsPatch
{

  internal readonly struct MenuItem(Type node, string? group = null, int? wireIndex = null, Type? binding = null, string? name = null, bool overload = false, Func<ProtoFluxNode, ProtoFluxElementProxy, ProtoFluxTool, bool>? onNodeSpawn = null) : IPageItems
  {
    internal readonly Type node = node;

    internal readonly Type? binding = binding;

    internal readonly string? name = name;

    internal readonly bool overload = overload;

    // Group similar items together based on a group name
    internal readonly string? group = group ?? "";

    internal readonly int wireIndex = wireIndex ?? 0;

    //                     Spawned Node   Held Element Proxy     Tool           Should still handle connection
    internal readonly Func<ProtoFluxNode, ProtoFluxElementProxy, ProtoFluxTool, bool>? onNodeSpawn = onNodeSpawn;

    internal readonly string DisplayName => name ?? NodeMetadataHelper.GetMetadata(node).Name ?? node.GetNiceTypeName();

    public string? GetGroup() => group;
    public string? GetDisplayName() => DisplayName;
    public Type? GetNodeType() => node;
  }

  internal static bool GetSelectionActions(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy)
  {
    var items = MenuItems(tool)
            .Where(i => (i.binding ?? i.node).IsValidGenericType(validForInstantiation: true)) // this isn't great, we should instead catch errors before they propigate to here.
            .ToList();
    var customItems = FluxRecipeConfig.GetItems(tool, elementProxy).ToList();
    // todo: pages / menu

    if (items.Count + customItems.Count == 0) return true;

    Action<ProtoFluxTool, ProtoFluxElementProxy, MenuItem, ProtoFluxNode>? currentAction = null;
    colorX? targetColor = null;

    switch (elementProxy)
    {
      case ProtoFluxInputProxy inputProxy:
        {
          currentAction = ProcessInputProxyItem;
          targetColor = inputProxy.InputType.Value.GetTypeColor();
          break;
        }
      case ProtoFluxOutputProxy outputProxy:
        {
          currentAction = ProcessOutputProxyItem;
          targetColor = outputProxy.OutputType.Value.GetTypeColor();
          break;
        }
      case ProtoFluxImpulseProxy impulseProxy:
        {
          currentAction = ProcessImpulseProxyItem;
          break;
        }
      case ProtoFluxOperationProxy operationProxy:
        {
          currentAction = ProcessOperationProxyItem;
          break;
        }
      default:
        {
          currentAction = ProcessEmptyProxyItem;
          break;
        }
    }

    if (currentAction == null) return true;

    Pager<MenuItem> baseItemManager = new();
    Pager<FluxRecipeConfig.PartialMenuItem> customItemManager = new();

    baseItemManager.InitPagedItems(items, targetColor, elementProxy, (Tool, Proxy, Item) =>
    {
      BaseMenuSetup(Tool, Proxy, Item, currentAction);
    });
    customItemManager.InitPagedItems(customItems, targetColor, elementProxy, ProcessCustomProxyItem);

    var rootData = new PageRootData(baseItemManager, customItemManager);

    CreateRootItems(tool, rootData);

    return false;
  }

  #region Proxy Setups

  private static void ProcessInputProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy, MenuItem item, ProtoFluxNode addedNode)
  {
    ProtoFluxInputProxy inputProxy = (ProtoFluxInputProxy)elementProxy;
    if (item.overload)
    {
      tool.StartTask(async () =>
      {
        // this is dumb
        // TODO: investigate why it's needed to avoid the one or two update disconnect issue
        await new Updates(1);

        if (item.onNodeSpawn != null)
        {
          bool doConnect = item.onNodeSpawn(addedNode, elementProxy, tool);

          if (!doConnect) return;
        }

        var output = addedNode.GetOutput(item.wireIndex); // this should be checked to ensure it isnt outside of the bounds
        elementProxy.Node.Target.TryConnectInput(inputProxy.NodeInput.Target, output, allowExplicitCast: false, undoable: true);
      });
    }
    else
    {
      if (item.onNodeSpawn != null)
      {
        bool doConnect = item.onNodeSpawn(addedNode, elementProxy, tool);

        if (!doConnect) return;
      }

      var output = addedNode.NodeOutputs
        .FirstOrDefault(o => typeof(INodeOutput<>).MakeGenericType(inputProxy.InputType).IsAssignableFrom(o.GetType()))
        ?? throw new Exception($"Could not find matching output of type '{inputProxy.InputType}' in '{addedNode}'");

      elementProxy.Node.Target.TryConnectInput(inputProxy.NodeInput.Target, output, allowExplicitCast: false, undoable: true);
    }
  }
  private static void ProcessOutputProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy, MenuItem item, ProtoFluxNode addedNode)
  {
    ProtoFluxOutputProxy outputProxy = (ProtoFluxOutputProxy)elementProxy;
    if (item.overload) throw new Exception("Overloading with ProtoFluxOutputProxy is not supported");
    var input = addedNode.NodeInputs
      .FirstOrDefault(i => i.TargetType.IsGenericType && (outputProxy.OutputType.Value.IsAssignableFrom(i.TargetType.GenericTypeArguments[0]) || ProtoFlux.Core.TypeHelper.CanImplicitlyConvertTo(outputProxy.OutputType, i.TargetType.GenericTypeArguments[0])))
      ?? throw new Exception($"Could not find matching input of type '{outputProxy.OutputType}' in '{addedNode}'");

    tool.StartTask(async () =>
    {
      // this is dumb
      // TODO: investigate why it's needed for casting to work
      await new Updates();

      if (item.onNodeSpawn != null)
      {
        bool doConnect = item.onNodeSpawn(addedNode, elementProxy, tool);

        if (!doConnect) return;
      }

      addedNode.TryConnectInput(input, outputProxy.NodeOutput.Target, allowExplicitCast: false, undoable: true);
    });
  }
  private static void ProcessImpulseProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy, MenuItem item, ProtoFluxNode addedNode)
  {
    ProtoFluxImpulseProxy impulseProxy = (ProtoFluxImpulseProxy)elementProxy;
    if (item.overload) throw new Exception("Overloading with ProtoFluxImpulseProxy is not supported");
    if (item.onNodeSpawn != null)
    {
      bool doConnect = item.onNodeSpawn(addedNode, elementProxy, tool);

      if (!doConnect) return;
    }

    INodeOperation? targetOperation = null;

    int nodeOpCount = addedNode.NodeOperationCount;
    if (nodeOpCount > 0)
    {
      if (item.wireIndex < nodeOpCount) // if not provided, 0 defaults, so this is probably fine
      {
        targetOperation = addedNode.GetOperation(item.wireIndex);
      }
    }
    else
    {
      ISyncList OpList = addedNode.GetOperationList(0);
      if (item.wireIndex < OpList.Count)
      {
        targetOperation = OpList.GetElement(item.wireIndex) as INodeOperation;
      }
    }

    if (targetOperation == null) return;

    addedNode.TryConnectImpulse(impulseProxy.NodeImpulse.Target, targetOperation, undoable: true);
  }
  private static void ProcessOperationProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy, MenuItem item, ProtoFluxNode addedNode)
  {
    ProtoFluxOperationProxy operationProxy = (ProtoFluxOperationProxy)elementProxy;
    if (item.overload) throw new Exception("Overloading with ProtoFluxOperationProxy is not supported");
    if (item.onNodeSpawn != null)
    {
      bool doConnect = item.onNodeSpawn(addedNode, elementProxy, tool);

      if (!doConnect) return;
    }
    // this also needs to be checked for index out of range
    addedNode.TryConnectImpulse(addedNode.GetImpulse(item.wireIndex), operationProxy.NodeOperation.Target, undoable: true);
  }

  private static void ProcessEmptyProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy elementProxy, MenuItem item, ProtoFluxNode addedNode)
  {
    item.onNodeSpawn?.Invoke(addedNode, elementProxy, tool);
  }

  private static void ProcessCustomProxyItem(ProtoFluxTool tool, ProtoFluxElementProxy proxy, FluxRecipeConfig.PartialMenuItem item)
  {
    item.onMenuPress(tool, proxy, item.recipe);
    tool.LocalUser.CloseContextMenu(tool);
    CleanupDraggedWire(tool);
  }

  #endregion

  #region Groups and Pages

  internal const int MAX_PER_PAGE = 8;

  #region Groups

  internal struct PageRootData(Pager<MenuItem> mainItems, Pager<FluxRecipeConfig.PartialMenuItem> customItems)
  {
    internal Pager<MenuItem> mainItems = mainItems;
    internal Pager<FluxRecipeConfig.PartialMenuItem> customItems = customItems;
  }

  internal static void CreateRootItems(ProtoFluxTool tool, PageRootData rootData)
  {
    tool.StartTask(async () =>
    {
      var menu = await CreateContext(tool);

      var baseItemManager = rootData.mainItems;

      baseItemManager.CreateGroups(tool, menu, colorX.White, rootData);


      var customItemManager = rootData.customItems;

      customItemManager.CreateGroups(tool, menu, colorX.Orange, rootData, "Custom");
    });
  }

  #endregion

  #region Utils

  private static async Task<ContextMenu> CreateContext(this ProtoFluxTool tool)
  {
    var newMenu = await tool.LocalUser.OpenContextMenu(tool, tool.Slot);
    Traverse.Create(newMenu).Field<float?>("_speedOverride").Value = 10; // Should allow for consistent flicking
    return newMenu;
  }
  private static List<List<T>> Split<T>(IList<T> source)
  {
    return source
      .Select((x, i) => new { Index = i, Value = x })
      .GroupBy(x => x.Index / MAX_PER_PAGE)
      .Select(x => x.Select(v => v.Value).ToList())
      .ToList();
  }

  

  private static void BaseMenuSetup(ProtoFluxTool tool, ProtoFluxElementProxy proxy, MenuItem item, Action<ProtoFluxTool, ProtoFluxElementProxy, MenuItem, ProtoFluxNode> setup)
  {
    var nodeBinding = item.binding ?? ProtoFluxHelper.GetBindingForNode(item.node);
    tool.SpawnNode(nodeBinding, n =>
    {
      n.EnsureElementsInDynamicLists();
      setup(tool, proxy, item, n);
      tool.LocalUser.CloseContextMenu(tool);
      CleanupDraggedWire(tool);
    });
  }

  #endregion

  #endregion

  // note: if we can build up a graph then we can egraph reduce to make matches like this easier to spot automatically rather than needing to check each one manually
  // todo: detect add + 1 and offer to convert to inc?
  // todo: detect add + 1 or inc and write and offer to convert to increment?

  internal static IEnumerable<MenuItem> MenuItems(ProtoFluxTool __instance)
  {
    var _currentProxy = Traverse.Create(__instance).Field("_currentProxy").GetValue<SyncRef<ProtoFluxElementProxy>>();
    var target = _currentProxy?.Target;

    foreach (var item in GeneralNumericOperationMenuItems(target)) yield return item;
    foreach (var item in GeneralObjectOperationMenuItems(target)) yield return item;

    if (target is ProtoFluxInputProxy inputProxy)
    {
      foreach (var item in InputMenuItems(inputProxy)) yield return item;
    }

    else if (target is ProtoFluxOutputProxy outputProxy)
    {
      foreach (var item in OutputMenuItems(outputProxy)) yield return item;
    }

    else if (target is ProtoFluxImpulseProxy impulseProxy)
    {
      foreach (var item in ImpulseMenuItems(impulseProxy)) yield return item;
    }

    else if (target is ProtoFluxOperationProxy operationProxy)
    {
      foreach (var item in OperationMenuItems(operationProxy)) yield return item;
    }

    else
    {
      foreach (var item in NullProxyMenuItems()) yield return item;
    }
  }

  private static IEnumerable<MenuItem> ImpulseMenuItems(ProtoFluxImpulseProxy impulseProxy)
  {
    var nodeType = impulseProxy.Node.Target.NodeType;

    // TODO: convert to while?
    yield return new MenuItem(typeof(For), group: "Impulse");
    yield return new MenuItem(typeof(If), group: "Impulse");
    yield return new MenuItem(typeof(ValueWrite<dummy>), group: "Variables");
    yield return new MenuItem(typeof(Sequence), group: "Impulse");
    yield return new MenuItem(typeof(DynamicImpulseTrigger), group: "Impulse");
    yield return new MenuItem(typeof(StartAsyncTask), group: "Impulse");
    yield return new MenuItem(typeof(ImpulseDemultiplexer), name: "Impulse Demultiplex", group: "Selection");

    yield return new MenuItem(typeof(DataModelBooleanToggle), group: "Variables");

    if (IsIterationNode(nodeType))
    {
      yield return new MenuItem(typeof(ValueIncrement<int>), group: "Variables"); // dec can be swapped to?
      yield return new MenuItem(typeof(ValueDecrement<int>), group: "Variables"); // dec can be swapped to?
    }

    else if (nodeType == typeof(DuplicateSlot))
    {
      yield return new MenuItem(typeof(SetGlobalTransform), group: "Transform");
      yield return new MenuItem(typeof(SetLocalTransform), group: "Transform");
      yield return new MenuItem(typeof(SetSlotPersistentSelf), group: "Slot Operations");
      // yes, this can be casted from SetSlotPersistentSelf. Considering we have groups however, its better to make it a little simpler.
      yield return new MenuItem(typeof(SetSlotActiveSelf), group: "Slot Operations");
    }

    else if (nodeType == typeof(RenderToTextureAsset))
    {
      yield return new MenuItem(typeof(AttachTexture2D), group: "Assets");
      yield return new MenuItem(typeof(AttachSprite), group: "Assets");
    }

    else if (nodeType == typeof(ImpulseDemultiplexer))
    {
      yield return new MenuItem(typeof(ImpulseMultiplexer), name: "Impulse Multiplex", group: "Selection");
    }

    switch (impulseProxy.ImpulseType.Value)
    {
      case ImpulseType.AsyncCall:
      case ImpulseType.AsyncResumption:
        yield return new MenuItem(typeof(AsyncFor), group: "Async Impulse");
        yield return new MenuItem(typeof(AsyncSequence), group: "Async Impulse");
        yield return new MenuItem(typeof(DelayUpdates), group: "Async Impulse");
        yield return new MenuItem(typeof(DelaySecondsFloat), group: "Async Impulse");
        yield return new MenuItem(typeof(AsyncDynamicImpulseTrigger), group: "Async Impulse");
        break;
    }
  }

  private static IEnumerable<MenuItem> OperationMenuItems(ProtoFluxOperationProxy operationProxy)
  {
    yield return new MenuItem(typeof(FireOnTrue), group: "Impulse");
    yield return new MenuItem(typeof(FireOnFalse), group: "Impulse");
    yield return new MenuItem(typeof(FireOnValueChange<bool>), group: "Impulse");

    yield return new MenuItem(typeof(DynamicImpulseReceiver), group: "Impulse");

    yield return new MenuItem(typeof(FireWhileTrue), group: "Loops");

    yield return new MenuItem(typeof(SecondsTimer), group: "Loops");

    yield return new MenuItem(typeof(Update), group: "Loops");
    yield return new MenuItem(typeof(LocalUpdate), group: "Loops");

    yield return new MenuItem(typeof(AsyncDynamicImpulseReceiver), group: "Async Impulse");
    yield return new MenuItem(typeof(StartAsyncTask), group: "Async Impulse");


    yield return new MenuItem(typeof(OnLoaded), group: "Events");
    yield return new MenuItem(typeof(OnSaving), group: "Events");
    yield return new MenuItem(typeof(OnStart), group: "Events");
    yield return new MenuItem(typeof(OnDuplicate), group: "Events");
    yield return new MenuItem(typeof(OnDestroy), group: "Events");
    yield return new MenuItem(typeof(OnDestroying), group: "Events");
    yield return new MenuItem(typeof(OnPackageImported), group: "Events");
  }

  internal static IEnumerable<MenuItem> GeneralNumericOperationMenuItems(ProtoFluxElementProxy? target)
  {
    {
      // TODO: It's nice to have these work with any node, I think their precedence should be lower than manually specified ones and potentially hidden by default for many types that support but do not need, esp. comparison.
      //       When I'm more sure that Swapping won't world crash I think I can limit comparison to a single node and then swap to the right one as a sort of submenu?
      //       Feels a little weird though, ux is difficult. A custom uix menu could help.
      if (target is ProtoFluxOutputProxy { OutputType.Value: var outputType } && (outputType.IsUnmanaged() || typeof(ISphericalHarmonics).IsAssignableFrom(outputType)))
      {
        var world = target.World;
        var coder = Traverse.Create(typeof(Coder<>).MakeGenericType(outputType));
        var isMatrix = outputType.IsMatrixType();
        var isQuaternion = outputType.IsQuaternionType();
        var psuedoGenericTypes = world.GetPsuedoGenericTypesForWorld();
        // only handle values

        if (isQuaternion)
        {
          if (TryGetPsuedoGenericForType(world, "Slerp_", outputType) is Type slerpType)
          {
            yield return new MenuItem(slerpType, group: "Lerp");
          }

          if (TryGetPsuedoGenericForType(world, "Pow_", outputType) is Type powType)
          {
            yield return new MenuItem(powType, group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsMul").Value)
          {
            yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(outputType), group: "Basic Math");
          }

          if (coder.Property<bool>("SupportsDiv").Value)
          {
            yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(outputType), group: "Basic Math");
          }
        }
        else
        {
          if (coder.Property<bool>("SupportsAddSub").Value)
          {
            yield return new MenuItem(typeof(ValueAdd<>).MakeGenericType(outputType), group: "Basic Math");
            yield return new MenuItem(typeof(ValueSub<>).MakeGenericType(outputType), group: "Basic Math");

            yield return new MenuItem(typeof(ValueInc<>).MakeGenericType(outputType), group: "Math Operations");
            yield return new MenuItem(typeof(ValueDec<>).MakeGenericType(outputType), group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsMul").Value)
          {
            yield return new MenuItem(typeof(ValueMul<>).MakeGenericType(outputType), group: "Basic Math");
            yield return new MenuItem(typeof(ValueSquare<>).MakeGenericType(outputType), group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsDiv").Value)
          {
            yield return new MenuItem(typeof(ValueDiv<>).MakeGenericType(outputType), group: "Basic Math");
          }

          if (coder.Property<bool>("SupportsNegate").Value)
          {
            yield return new MenuItem(typeof(ValueNegate<>).MakeGenericType(outputType), group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsMod").Value)
          {
            yield return new MenuItem(typeof(ValueMod<>).MakeGenericType(outputType), group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsAbs").Value && !isMatrix)
          {
            yield return new MenuItem(typeof(ValueAbs<>).MakeGenericType(outputType), group: "Math Operations");
          }

          if (coder.Property<bool>("SupportsComparison").Value)
          {
            // yield return new MenuItem(typeof(ValueLessThan<>).MakeGenericType(outputType));
            // yield return new MenuItem(typeof(ValueLessOrEqual<>).MakeGenericType(outputType));
            // yield return new MenuItem(typeof(ValueGreaterThan<>).MakeGenericType(outputType));
            // yield return new MenuItem(typeof(ValueGreaterOrEqual<>).MakeGenericType(outputType));
            //yield return new MenuItem(typeof(ValueEquals<>).MakeGenericType(outputType));
            // yield return new MenuItem(typeof(ValueNotEquals<>).MakeGenericType(outputType));
          }

          if (coder.Property<bool>("SupportsLerp").Value)
          {
            yield return new MenuItem(typeof(ValueLerp<>).MakeGenericType(outputType), group: "Lerp");
          }
          if (coder.Property<bool>("SupportsSmoothLerp").Value)
          {
            yield return new MenuItem(typeof(ValueSmoothLerp<>).MakeGenericType(outputType), group: "Lerp");
          }
        }


        if (TryGetInverseNode(outputType, out var inverseNodeType))
        {
          yield return new MenuItem(inverseNodeType, group: "Conversion");
        }

        if (TryGetTransposeNode(outputType, out var transposeNodeType))
        {
          yield return new MenuItem(transposeNodeType, name: "Transpose", group: "Conversion");
        }

        if (psuedoGenericTypes.Mask.Any(n => n.Types.First() == outputType))
        {
          yield return new(psuedoGenericTypes.Mask.First(n => n.Types.First() == outputType).Node, group: "Selection");
        }
      }
    }
  }

  internal static IEnumerable<MenuItem> GeneralObjectOperationMenuItems(ProtoFluxElementProxy? target)
  {
    if (target is ProtoFluxOutputProxy { OutputType.Value: var outputType } && !outputType.IsUnmanaged())
    {
      var coder = Traverse.Create(typeof(Coder<>).MakeGenericType(outputType));

      //if (coder.Property<bool>("SupportsComparison").Value)
      //{
      //yield return new MenuItem(typeof(ObjectEquals<>).MakeGenericType(outputType));
      //}

      yield break;
    }
  }


  private static Type? GetIVariableValueType(Type type)
  {
    if (TypeUtils.MatchInterface(type, typeof(IVariable<,>), out var varType))
    {
      return varType.GenericTypeArguments[1];
    }
    return null;
  }

  #region Output Items
  /// <summary>
  /// Yields menu items when holding an output wire. 
  /// </summary>
  /// <param name="outputProxy"></param>
  /// <returns></returns>
  internal static IEnumerable<MenuItem> OutputMenuItems(ProtoFluxOutputProxy outputProxy)
  {
    var world = outputProxy.World;
    var nodeType = outputProxy.Node.Target.NodeType;
    var psuedoGenericTypes = world.GetPsuedoGenericTypesForWorld();

    if (TryGetUnpackNode(outputProxy.World, outputProxy.OutputType, out var unpackNodeTypes))
    {
      foreach (var unpackNodeType in unpackNodeTypes)
      {
        yield return new MenuItem(unpackNodeType, group: "Packing");
      }
    }
    var outputType = outputProxy.OutputType.Value;

    var equalsNode = GetNodeForType(outputType, [
      new NodeTypeRecord(typeof(ValueEquals<>), null, null),
      new NodeTypeRecord(typeof(ObjectEquals<>), null, null),
    ]);
    yield return new MenuItem(equalsNode, group: "Comparisons");

    var firstMatchNode = GetNodeForType(outputType, [
      new NodeTypeRecord(typeof(IndexOfFirstValueMatch<>), null, null),
      new NodeTypeRecord(typeof(IndexOfFirstObjectMatch<>), null, null),
    ]);
    yield return new MenuItem(firstMatchNode, group: "Selection");


    var conditionalNode = GetNodeForType(outputType, [
        new NodeTypeRecord(typeof(ValueConditional<>), null, null),
            new NodeTypeRecord(typeof(ObjectConditional<>), null, null),
        ]);
    yield return new MenuItem(conditionalNode, group: "Selection");


    if (outputType == typeof(bool))
    {
      yield return new MenuItem(typeof(FireOnTrue), group: "Impulse");
      yield return new MenuItem(typeof(FireOnFalse), group: "Impulse");
      //yield return new MenuItem(typeof(FireOnValueChange<bool>));
    }
    var changeVariableNode = GetNodeForType(outputType, [
        new NodeTypeRecord(typeof(FireOnValueChange<>), null, null),
            new NodeTypeRecord(typeof(FireOnObjectValueChange<>), null, null),
            new NodeTypeRecord(typeof(FireOnRefChange<>), null, null),
        ]);
    yield return new MenuItem(changeVariableNode, group: "Impulse");
    var localChangeVariableNode = GetNodeForType(outputType, [
        new NodeTypeRecord(typeof(FireOnLocalValueChange<>), null, null),
            new NodeTypeRecord(typeof(FireOnLocalObjectChange<>), null, null),
        ]);
    yield return new MenuItem(localChangeVariableNode, group: "Impulse");

    if (!outputType.IsValueType)
    {
      yield return new MenuItem(typeof(IsNull<>).MakeGenericType(outputType), group: "Comparisons");
      yield return new MenuItem(typeof(NotNull<>).MakeGenericType(outputType), group: "Comparisons");
      yield return new MenuItem(typeof(NullCoalesce<>).MakeGenericType(outputType), group: "Selection");
    }


    if (outputType == typeof(Slot))
    {
      yield return new MenuItem(typeof(SetGlobalTransform), group: "Slot Operations");

      if (nodeType == typeof(DuplicateSlot))
      {
        yield return new MenuItem(typeof(SetSlotPersistentSelf), group: "Slot Operations");
        yield return new MenuItem(typeof(SetSlotActiveSelf), group: "Slot Operations");
      }

      yield return new MenuItem(typeof(GetParentSlot), group: "Slot Info");
      yield return new MenuItem(typeof(SetParent), group: "Slot Operations");
      yield return new MenuItem(typeof(GlobalTransform), group: "Slot Info");
      yield return new MenuItem(typeof(GetForward), group: "Slot Info");
      yield return new MenuItem(typeof(GetChild), group: "Slot Operations");
      yield return new MenuItem(typeof(ChildrenCount), group: "Slot Info");
      yield return new MenuItem(typeof(FindChildByTag), group: "Slot Operations"); // use tag here because it has less inputs which fits better when going to swap.
      yield return new MenuItem(typeof(GetSlotName), group: "Slot Info");
      yield return new MenuItem(typeof(GetObjectRoot), group: "Slot Info");
      yield return new MenuItem(typeof(GetSlotActive), group: "Slot Info");
      yield return new MenuItem(typeof(GetActiveUser), group: "Slot Info");

      yield return new MenuItem(typeof(DuplicateSlot), group: "Slot Operations");
      yield return new MenuItem(typeof(DestroySlot), group: "Slot Operations");
      yield return new MenuItem(typeof(DestroySlotChildren), group: "Slot Operations");


      yield return new MenuItem(typeof(DynamicImpulseTrigger), group: "Impulse");

      // This is why we have a setup() on the MenuItem.
      // Is there a better solution? probably.
      // But until i find that solution, this is what we deal with.
      // 2 nodes: ObjectCast -> IWorldElement > AllocatingUser
      yield return new MenuItem(typeof(ObjectCast<Slot, IWorldElement>), name: "Allocating User", onNodeSpawn: (ProtoFluxNode node, ProtoFluxElementProxy proxy, ProtoFluxTool tool) =>
      {
        tool.StartTask(async () =>
        {
          // Node spawning
          Type allocNode = typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.References.AllocatingUser);

          ProtoFluxNode? thisAllocNode = null;

          tool.SpawnNode(allocNode, newNode =>
          {
            thisAllocNode = newNode;
            newNode.EnsureVisual();
          });

          await new Updates(3);

          if (thisAllocNode == null)
          {
            node.Slot.Destroy();
            return;
          }

          node.World.BeginUndoBatch("Create Allocating User");

          node.Slot.CreateSpawnUndoPoint("Spawn Object Cast");
          thisAllocNode.Slot.CreateSpawnUndoPoint("Spawn Allocating User");

          // Inputs and outputs

          INodeOutput inputRelay = node.GetOutput(0);

          ISyncRef allocInstance = thisAllocNode.GetInput(0);

          allocInstance.Target = inputRelay;

          // Positions
          float3 baseUp = node.Slot.Up;
          float3 baseRight = node.Slot.Right;

          void LocalTransformNode(ProtoFluxNode input, float X, float Y)
          {
            Slot target = input.Slot;
            target.CopyTransform(node.Slot);
            target.GlobalPosition += (baseUp * Y) + (baseRight * X);
          }

          LocalTransformNode(thisAllocNode, 0.09f, 0.00375f);

          node.World.EndUndoBatch();
        });

        return true;

      }, group: "Slot Info");

      // 5 nodes: (Relay 1 > Children Count > For [iterations] > Get Child [index]) + (Relay 1 > Relay 2 > Get Child [instance])
      yield return new MenuItem(typeof(ObjectRelay<Slot>), name: "Foreach Child", onNodeSpawn: (ProtoFluxNode node, ProtoFluxElementProxy proxy, ProtoFluxTool tool) =>
      {
        tool.StartTask(async () =>
        {
          // Node spawning
          Type childCountNode = typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots.ChildrenCount);
          Type forNode = typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.For);
          Type getChildNode = typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots.GetChild);
          Type relayNode = typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.ObjectRelay<Slot>);

          ProtoFluxNode thisChildCountNode = null;
          ProtoFluxNode thisForNode = null;
          ProtoFluxNode thisGetChild = null;
          ProtoFluxNode thisRelayNode = null;

          tool.SpawnNode(childCountNode, newNode =>
          {
            thisChildCountNode = newNode;
            newNode.EnsureVisual();
          });
          tool.SpawnNode(forNode, newNode =>
          {
            thisForNode = newNode;
            newNode.EnsureVisual();
          });
          tool.SpawnNode(getChildNode, newNode =>
          {
            thisGetChild = newNode;
            newNode.EnsureVisual();
          });
          tool.SpawnNode(relayNode, newNode =>
          {
            thisRelayNode = newNode;
            newNode.EnsureVisual();
          });

          await new Updates(3);

          if (thisChildCountNode == null) return;
          if (thisForNode == null) return;
          if (thisGetChild == null) return;
          if (thisRelayNode == null) return;

          node.World.BeginUndoBatch("Create Foreach Child");

          node.Slot.CreateSpawnUndoPoint("Spawn Child Count");
          thisChildCountNode.Slot.CreateSpawnUndoPoint("Spawn Child Count");
          thisForNode.Slot.CreateSpawnUndoPoint("Spawn For");
          thisGetChild.Slot.CreateSpawnUndoPoint("Spawn Get Child");
          thisRelayNode.Slot.CreateSpawnUndoPoint("Spawn Relay");

          // Inputs and outputs

          INodeOutput inputRelay = node.GetOutput(0);

          ISyncRef childCountInstance = thisChildCountNode.GetInput(0);
          INodeOutput childCount = thisChildCountNode.GetOutput(0);

          ISyncRef forCount = thisForNode.GetInput(0);
          INodeOutput forIndex = thisForNode.GetOutput(0);

          ISyncRef childInstance = thisGetChild.GetInput(0);
          ISyncRef childIndex = thisGetChild.GetInput(1);

          ISyncRef relayInstance = thisRelayNode.GetInput(0);
          INodeOutput relayOutput = thisRelayNode.GetOutput(0);



          relayInstance.Target = inputRelay;
          childCountInstance.Target = inputRelay;

          forCount.Target = childCount;

          childIndex.Target = forIndex;
          childInstance.Target = relayOutput;

          // Positions
          float3 baseUp = node.Slot.Up;
          float3 baseRight = node.Slot.Right;

          void LocalTransformNode(ProtoFluxNode input, float X, float Y)
          {
            Slot target = input.Slot;
            target.CopyTransform(node.Slot);
            target.GlobalPosition += (baseUp * Y) + (baseRight * X);
          }

          LocalTransformNode(thisChildCountNode, 0.12f, 0.00375f);
          LocalTransformNode(thisForNode, 0.27f, -0.01125f);

          LocalTransformNode(thisRelayNode, 0.075f, -0.105f);

          LocalTransformNode(thisGetChild, 0.42f, -0.11625f);

          node.World.EndUndoBatch();
        });

        return true;

      }, group: "Slot Operations");

    }

    else if (outputType == typeof(bool))
    {
      yield return new MenuItem(typeof(If), group: "Impulse");
      //yield return new MenuItem(typeof(AND_Bool), group: ProxyTypeName);
      //yield return new MenuItem(typeof(OR_Bool), group: ProxyTypeName);
      //yield return new MenuItem(typeof(NOT_Bool), group: ProxyTypeName);
    }
    if (outputType == typeof(bool) || outputType == typeof(bool2) || outputType == typeof(bool3) || outputType == typeof(bool4))
    {
      yield return new(psuedoGenericTypes.AND.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");
      yield return new(psuedoGenericTypes.OR.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");
      yield return new(psuedoGenericTypes.NOT.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");

      if (outputType != typeof(bool))
      {
        yield return new(psuedoGenericTypes.All.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");
        yield return new(psuedoGenericTypes.Any.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");
        yield return new(psuedoGenericTypes.None.First(n => n.Types.First() == outputType).Node, group: "Bool Logic");
      }
    }

    else if (outputType == typeof(string))
    {
      yield return new MenuItem(typeof(GetCharacter), group: "String Info");
      yield return new MenuItem(typeof(StringLength), group: "String Info");
      yield return new MenuItem(typeof(CountOccurrences), group: "String Info");
      yield return new MenuItem(typeof(IndexOfString), group: "String Info");
      yield return new MenuItem(typeof(Contains), group: "String Info");
      yield return new MenuItem(typeof(Substring), group: "String Operations");
      yield return new MenuItem(typeof(FormatString), group: "String Operations");
      yield return new MenuItem(typeof(StripRTF_Tags), group: "String Operations");

      yield return new MenuItem(typeof(ConcatenateString), group: "String Operations");
    }

    else if (outputType == typeof(DateTime))
    {
      yield return new MenuItem(typeof(Sub_DateTime), group: "Date Time");
      yield return new MenuItem(typeof(Add_DateTime_TimeSpan), group: "Date Time");
    }

    else if (outputType == typeof(BoundingBox))
    {
      yield return new MenuItem(typeof(EncapsulateBounds), group: "Bounds");
      yield return new MenuItem(typeof(EncapsulatePoint), group: "Bounds");
      yield return new MenuItem(typeof(TransformBounds), group: "Bounds");
      yield return new MenuItem(typeof(BoundingBoxProperties), group: "Bounds");
    }

    else if (outputType == typeof(Camera))
    {
      yield return new(typeof(RenderToTextureAsset), group: "Assets");
    }

    else if (outputType == typeof(int) && (IsIterationNode(nodeType) || nodeType == typeof(IndexOfString)))
    {
      yield return new MenuItem(typeof(ValueInc<int>), group: "Variables");
      yield return new MenuItem(typeof(ValueDec<int>), group: "Variables");
    }

    if (outputType == typeof(UserRef))
    {
      yield return new MenuItem(typeof(UserRefAsVariable), group: "User Refs");
    }

    if (outputType == typeof(UserRoot))
    {
      yield return new MenuItem(typeof(ActiveUserRootUser), group: "User Root");
      yield return new MenuItem(typeof(UserRootGlobalScale), group: "User Root");
      yield return new MenuItem(typeof(HeadSlot), group: "User Root");
      yield return new MenuItem(typeof(HeadPosition), group: "User Root");
      yield return new MenuItem(typeof(HeadRotation), group: "User Root");
    }

    if (outputType == typeof(IWorldElement))
    {
      yield return new MenuItem(typeof(AllocatingUser), group: "World Elements");
      yield return new MenuItem(typeof(ReferenceID), group: "World Elements");
      yield return new MenuItem(typeof(IsRemoved), group: "World Elements");
    }

    if (outputType == typeof(User))
    {
      yield return new MenuItem(typeof(UserUsername), group: "Users");
      yield return new MenuItem(typeof(UserUserID), group: "Users");
      yield return new MenuItem(typeof(IsLocalUser), group: "Users");
      yield return new MenuItem(typeof(UserVR_Active), group: "Users");
      yield return new MenuItem(typeof(UserRootSlot), group: "Users");
      yield return new MenuItem(typeof(UserUserRoot), group: "Users");

      yield return new MenuItem(typeof(StandardController), group: "Input");

      // Controller type of this user
      // note: this probably isnt needed but i want to
      IStandardController controller = Engine.Current.WorldManager.FocusedWorld.LocalUser.InputInterface.GetControllerNode(Chirality.Right);
      Type? controllerType = controller.GetType();
      if (controllerType != null)
      {
        if (controllerType == typeof(FrooxEngine.TouchController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.TouchController), group: "Input");
        if (controllerType == typeof(FrooxEngine.IndexController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.IndexController), group: "Input");
        if (controllerType == typeof(FrooxEngine.HPReverbController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.HPReverbController), group: "Input");
        if (controllerType == typeof(FrooxEngine.ViveController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.ViveController), group: "Input");
        if (controllerType == typeof(FrooxEngine.CosmosController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.CosmosController), group: "Input");
        if (controllerType == typeof(FrooxEngine.WindowsMRController))
          yield return new MenuItem(typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.WindowsMRController), group: "Input");
      }
    }

    if (outputType == typeof(BodyNode))
    {
      yield return new MenuItem(typeof(BodyNodeSlot), group: "Body Nodes");
      yield return new MenuItem(typeof(BodyNodeChirality), group: "Body Nodes");
      yield return new MenuItem(typeof(OtherSide), group: "Body Nodes");
    }

    if (outputType == typeof(Grabber))
    {
      yield return new MenuItem(typeof(GrabberBodyNode), group: "Grabbers");
    }

    if (outputType == typeof(CharacterController))
    {
      yield return new MenuItem(typeof(CharacterLinearVelocity), group: "Controller Info");
      yield return new MenuItem(typeof(IsCharacterOnGround), group: "Controller Info");
      yield return new MenuItem(typeof(CharacterControllerUser), group: "Controller Info");

      yield return new MenuItem(typeof(SetCharacterVelocity), group: "Controller Operations");
      yield return new MenuItem(typeof(SetCharacterGravity), group: "Controller Operations");
      yield return new MenuItem(typeof(ApplyCharacterForce), group: "Controller Operations");
      yield return new MenuItem(typeof(ApplyCharacterImpulse), group: "Controller Operations");
    }

    if (outputType == typeof(Type))
    {
      yield return new MenuItem(typeof(TypeColor), group: "Types");
      yield return new MenuItem(typeof(NiceTypeName), group: "Types");
    }

    if (outputType == typeof(Key))
    {
      yield return new MenuItem(typeof(KeyHeld), group: "Keys");
    }

    if (outputType == typeof(object))
    {
      yield return new MenuItem(typeof(ToString_object), group: "String Operations");
    }

    if (outputType == typeof(bool) || outputType == typeof(bool2) || outputType == typeof(bool3) || outputType == typeof(bool4))
    {
      yield return new(psuedoGenericTypes.AND.First(n => n.Types.First() == outputType).Node);
      yield return new(psuedoGenericTypes.OR.First(n => n.Types.First() == outputType).Node);
      yield return new(psuedoGenericTypes.NOT.First(n => n.Types.First() == outputType).Node);
    }

    if (outputType.IsEnum)
    {
      yield return new MenuItem(typeof(NextValue<>).MakeGenericType(outputType), name: typeof(NextValue<>).GetNiceName(), group: "Enums");
      yield return new MenuItem(typeof(ShiftEnum<>).MakeGenericType(outputType), name: typeof(ShiftEnum<>).GetNiceName(), group: "Enums");
      yield return new MenuItem(typeof(TryEnumToInt<>).MakeGenericType(outputType), name: "TryEnumToInt<T>", group: "Enums");
      //yield return new MenuItem(typeof(ValueEquals<>).MakeGenericType(outputType));

      var enumType = outputType.GetEnumUnderlyingType();
      if (NodeUtils.TryGetEnumToNumberNode(enumType, out var toNumberType))
      {
        yield return new MenuItem(toNumberType.MakeGenericType(outputType), group: "Enums");
      }
    }

    if (TypeUtils.MatchInterface(outputType, typeof(IQuantity<>), out var quantityType))
    {
      var baseType = quantityType.GenericTypeArguments[0];
      yield return new MenuItem(typeof(BaseValue<>).MakeGenericType(baseType), group: "Quantities");
      yield return new MenuItem(typeof(FormatQuantity<>).MakeGenericType(baseType), group: "Quantities");
    }

    if (TypeUtils.MatchInterface(outputType, typeof(ICollider), out _))
    {
      yield return new MenuItem(typeof(IsCharacterController), group: "Casts");
      yield return new MenuItem(typeof(AsCharacterController), group: "Casts");
    }

    if (TypeUtils.MatchesType(typeof(IValue<>), outputType))
    {
      var typeArg = outputType.GenericTypeArguments[0];
      yield return new MenuItem(typeof(FieldAsVariable<>).MakeGenericType(typeArg), group: "Variables");
    }

    if (TypeUtils.MatchesType(typeof(ISyncRef<>), outputType))
    {
      var typeArg = outputType.GenericTypeArguments[0];
      yield return new MenuItem(typeof(ReferenceInterfaceAsVariable<>).MakeGenericType(typeArg), group: "Variables");
    }

    if (TypeUtils.MatchesType(typeof(SyncRef<>), outputType))
    {
      var typeArg = outputType.GenericTypeArguments[0];
      yield return new MenuItem(typeof(ReferenceAsVariable<>).MakeGenericType(typeArg), group: "Variables");
      yield return new MenuItem(typeof(ReferenceTarget<>).MakeGenericType(typeArg), group: "Variables");
    }

    if (TypeUtils.MatchInterface(outputType, typeof(IAssetProvider<AudioClip>), out _))
    {
      yield return new MenuItem(typeof(PlayOneShot), group: "Audio");
    }

    if (typeof(IComponent).IsAssignableFrom(outputType))
    {
      yield return new MenuItem(typeof(GetSlot), group: "Components");
      yield return new MenuItem(typeof(GetType), group: "Types");
    }

    if (typeof(IGrabbable).IsAssignableFrom(outputType))
    {
      yield return new MenuItem(typeof(IsGrabbableGrabbed), group: "Grabbable");
      yield return new MenuItem(typeof(IsGrabbableScalable), group: "Grabbable");
      yield return new MenuItem(typeof(IsGrabbableReceivable), group: "Grabbable");
      yield return new MenuItem(typeof(GrabbablePriority), group: "Grabbable");
      yield return new MenuItem(typeof(GrabbableGrabber), group: "Grabbable");
    }

    if (TypeUtils.MatchInterface(outputType, typeof(IAssetProvider<>), out var assetProviderType))
    {
      yield return new MenuItem(typeof(GetAsset<>).MakeGenericType(assetProviderType.GenericTypeArguments[0]), group: "Assets");
    }

    if (outputType == typeof(int) && (
        nodeType == typeof(ImpulseDemultiplexer)
        || TypeUtils.MatchesType(typeof(IndexOfFirstValueMatch<>), nodeType)
        || TypeUtils.MatchesType(typeof(IndexOfFirstObjectMatch<>), nodeType)
        ))
    {
      yield return new MenuItem(typeof(ValueMultiplex<dummy>), name: "Value Multiplex", group: "Selection");
      yield return new MenuItem(typeof(ImpulseMultiplexer), name: "Impulse Multiplex", group: "Selection");
      yield return new MenuItem(typeof(ValueDemultiplex<dummy>), name: "Value Demultiplex", group: "Selection");
    }

    if (nodeType == typeof(DataModelBooleanToggle) && outputType == typeof(bool))
    {
      yield return new(typeof(FireOnLocalValueChange<bool>), group: "Impulse");
    }

    if (Groups.MousePositionGroup.Contains(nodeType))
    {
      foreach (var node in Groups.ScreenPointGroup)
      {
        yield return new(node, group: "Inputs");
      }
    }

    if (Groups.WorldTimeFloatGroup.Contains(nodeType))
    {
      yield return new MenuItem(typeof(Sin_Float), group: "Math++");
    }
    else if (Groups.WorldTimeDoubleGroup.Contains(nodeType))
    {
      yield return new MenuItem(typeof(Sin_Double), group: "Math++");
    }

    if (TypeUtils.MatchesType(typeof(EnumToInt<>), nodeType) || TypeUtils.MatchesType(typeof(TryEnumToInt<>), nodeType))
    {
      yield return new MenuItem(typeof(ValueMultiplex<dummy>), group: "Selection");
    }

    if (nodeType == typeof(CountOccurrences) || nodeType == typeof(ChildrenCount) || nodeType == typeof(WorldUserCount))
    {
      yield return new MenuItem(typeof(For), group: "Impulse");
    }

    if (ContextualSwapActionsPatch.DeltaTimeGroup.Contains(nodeType.GetGenericTypeDefinitionOrSameType()))
    {
      foreach (var dtOperationType in ContextualSwapActionsPatch.DeltaTimeOperationGroup)
      {
        yield return new MenuItem(dtOperationType.MakeGenericType(typeof(float)), group: "Time");
      }
    }

    var outputNode = outputProxy.Node.Target.NodeInstance;
    Type? nodeVariable = GetIVariableValueType(outputNode.GetType());

    if (nodeVariable != null)
    {
      var variableInput = GetNodeForType(nodeVariable, [
        new NodeTypeRecord(typeof(ValueWrite<>), null, null),
        new NodeTypeRecord(typeof(ObjectWrite<>), null, null),
      ]);
      yield return new MenuItem(
        variableInput,
        onNodeSpawn: (ProtoFluxNode newNode, ProtoFluxElementProxy proxy, ProtoFluxTool _) =>
        {
          ProtoFluxOutputProxy output = (ProtoFluxOutputProxy)proxy;

          ISyncRef targetRef = newNode.GetReference(0);

          newNode.TryConnectReference(targetRef, outputProxy.Node.Target, undoable: true);

          return false;
        },
        group: "Variables"
      );
      var variableLatchInput = GetNodeForType(nodeVariable, [
        new NodeTypeRecord(typeof(ValueWriteLatch<>), null, null),
        new NodeTypeRecord(typeof(ObjectWriteLatch<>), null, null),
      ]);
      yield return new MenuItem(
        variableLatchInput,
        onNodeSpawn: (ProtoFluxNode newNode, ProtoFluxElementProxy proxy, ProtoFluxTool _) =>
        {
          ProtoFluxOutputProxy output = (ProtoFluxOutputProxy)proxy;

          ISyncRef targetRef = newNode.GetReference(0);

          newNode.TryConnectReference(targetRef, outputProxy.Node.Target, undoable: true);

          return false;
        },
        group: "Variables"
      );
    }
  }
  #endregion

  /// <summary>
  /// Generates menu items when holding an input wire.
  /// </summary>
  /// <param name="inputProxy"></param>
  /// <returns></returns>
  internal static IEnumerable<MenuItem> InputMenuItems(ProtoFluxInputProxy inputProxy)
  {
    var inputType = inputProxy.InputType.Value;
    var nodeType = inputProxy.Node.Target.NodeType;

    // one level deep check
    var nodeInstance = inputProxy.Node.Target.NodeInstance;
    var query = new NodeQueryAcceleration(nodeInstance.Runtime.Group);
    var indirectlyConnectsToIterationNode = query.GetEvaluatingNodes(nodeInstance).Any(n => IsIterationNode(n.GetType()));

    if (TryGetPackNode(inputProxy.World, inputType, out var packNodeTypes))
    {
      foreach (var packNodeType in packNodeTypes)
      {
        yield return new MenuItem(packNodeType, group: "Packing");
      }
    }

    if (inputType == typeof(string))
    {
      yield return new MenuItem(typeof(FormatString), group: "String Operations");
      yield return new MenuItem(typeof(ToString_object), group: "String Operations");
    }
    else if (inputType == typeof(User))
    {
      yield return new MenuItem(typeof(LocalUser), group: "Users");
      yield return new MenuItem(typeof(HostUser), group: "Users");
      yield return new MenuItem(typeof(UserFromUsername), group: "Users");
      yield return new MenuItem(typeof(GetActiveUser), group: "Users");
      yield return new MenuItem(typeof(GetActiveUserSelf), group: "Users");

      // Pick user
      List<User> users = [];
      inputProxy.Slot.World.GetUsers(users);
      foreach (User user in users)
      {
        yield return new MenuItem(
          typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.RefObjectInput<User>),
          name: user.UserName,
          onNodeSpawn: (node, proxy, tool) =>
          {
            var comp = node.Slot.GetComponent<FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.RefObjectInput<User>>();
            comp.Target.Target = user;
            return true;
          },
          binding: typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.RefObjectInput<User>),
          group: "User List"
        );
      }

      yield return new MenuItem(
        typeof(AllocatingUser),
        name: "Allocating User",
        binding: typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.References.AllocatingUser),
        group: "Slot Info");
    }

    else if (inputType == typeof(UserRoot))
    {
      yield return new MenuItem(typeof(GetActiveUserRoot), group: "User Root");
      yield return new MenuItem(typeof(LocalUserRoot), group: "User Root");
      yield return new MenuItem(typeof(UserUserRoot), group: "User Root");
    }

    else if (inputType == typeof(bool))
    {
      // I want to use dummy's here but it's not safe to do so.
      yield return new MenuItem(typeof(ValueLessThan<int>), group: "Comparisons");
      yield return new MenuItem(typeof(ValueLessOrEqual<int>), group: "Comparisons");
      yield return new MenuItem(typeof(ValueGreaterThan<int>), group: "Comparisons");
      yield return new MenuItem(typeof(ValueGreaterOrEqual<int>), group: "Comparisons");
      yield return new MenuItem(typeof(ValueEquals<int>), group: "Comparisons");
    }

    else if (inputType == typeof(DateTime))
    {
      yield return new MenuItem(typeof(UtcNow), group: "Date Time");
      yield return new MenuItem(typeof(FromUnixMilliseconds), group: "Date Time");
    }

    else if (inputType == typeof(TimeSpan))
    {
      yield return new MenuItem(typeof(Parse_TimeSpan), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromTicks), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromMilliseconds), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromSeconds), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromMinutes), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromHours), group: "Time Spans");
      yield return new MenuItem(typeof(TimeSpanFromDays), group: "Time Spans");
    }

    else if (inputType == typeof(Slot))
    {
      yield return new MenuItem(typeof(RootSlot), group: "Slots");
      yield return new MenuItem(typeof(LocalUserSlot), group: "Slots");
      yield return new MenuItem(typeof(LocalUserSpace), group: "Slots");
    }

    else if (inputType == typeof(BoundingBox))
    {
      yield return new MenuItem(typeof(ComputeBoundingBox), group: "Bounds");
      yield return new MenuItem(typeof(FromCenterSize), group: "Bounds");
      yield return new MenuItem(typeof(Empty), group: "Bounds");
      yield return new MenuItem(typeof(EncapsulateBounds), group: "Bounds");
      yield return new MenuItem(typeof(EncapsulatePoint), group: "Bounds");
      yield return new MenuItem(typeof(TransformBounds), group: "Bounds");
    }

    else if (inputType == typeof(CharacterController))
    {
      yield return new MenuItem(typeof(FindCharacterControllerFromSlot), group: "Character Controllers");
      yield return new MenuItem(typeof(FindCharacterControllerFromUser), group: "Character Controllers");
    }

    else if (inputType == typeof(Type))
    {
      yield return new MenuItem(typeof(GetType), group: "Types");
    }

    else if (inputType == typeof(Chirality))
    {
      yield return new MenuItem(typeof(BodyNodeChirality), group: "Chirality");
      yield return new MenuItem(typeof(ToolEquippingSide), group: "Chirality");
    }

    else if (inputType == typeof(BodyNode))
    {
      yield return new MenuItem(typeof(GrabberBodyNode), group: "Body Nodes");
    }

    else if (inputType == typeof(Grabber))
    {
      yield return new MenuItem(typeof(GetUserGrabber), group: "Grabbers");
      yield return new MenuItem(typeof(GrabbableGrabber), group: "Grabbers");
    }

    else if (inputType == typeof(Uri))
    {
      yield return new MenuItem(typeof(StringToAbsoluteURI), group: "Uri");
    }

    if (inputType == typeof(float))
    {
      yield return new MenuItem(typeof(WorldTimeFloat), group: "Time");
      yield return new MenuItem(typeof(WorldTime2Float), group: "Time");
      yield return new MenuItem(typeof(WorldTimeTenthFloat), group: "Time");
    }

    else if (TypeUtils.MatchInterface(inputType, typeof(IQuantity<>), out var quantityType))
    {
      var baseType = quantityType.GenericTypeArguments[0];
      yield return new MenuItem(typeof(FromBaseValue<>).MakeGenericType(baseType), group: "Quantities");
      yield return new MenuItem(typeof(ParseQuantity<>).MakeGenericType(baseType), group: "Quantities");
    }

    else if (inputProxy.ElementName == "B" && (nodeType == typeof(ValueMul<floatQ>) || nodeType == typeof(Mul_FloatQ_Float3)))
    {
      bool isFloatQ = nodeType == typeof(ValueMul<floatQ>);

      yield return new MenuItem(typeof(GetForward), group: "Directions", overload: isFloatQ);
      yield return new MenuItem(typeof(GetBackward), group: "Directions", overload: isFloatQ);
      yield return new MenuItem(typeof(GetUp), group: "Directions", overload: isFloatQ);
      yield return new MenuItem(typeof(GetDown), group: "Directions", overload: isFloatQ);
      yield return new MenuItem(typeof(GetLeft), group: "Directions", overload: isFloatQ);
      yield return new MenuItem(typeof(GetRight), group: "Directions", overload: isFloatQ);
      // yield return new MenuItem(
      //     name: "ValueInput<float>",
      //     node: typeof(ExternalValueInput<FrooxEngineContext, float3>),
      //     binding: typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.ValueInput<float3>),
      //     overload: true
      // );
    }

    else if (inputType == typeof(int) && (IsIterationNode(nodeType) || indirectlyConnectsToIterationNode))
    {
      yield return new MenuItem(typeof(ValueInc<int>), group: "Values");
      yield return new MenuItem(typeof(ValueDec<int>), group: "Values");
      yield return new MenuItem(typeof(ChildrenCount), group: "Values");
      yield return new MenuItem(typeof(CountOccurrences), group: "Values");
    }

    if (inputProxy.ElementName == nameof(LocalScreenPointToDirection.NormalizedScreenPoint))
    {
      yield return new MenuItem(typeof(NormalizedMousePosition), group: "Inputs");
    }

    if (TypeUtils.MatchInterface(inputType, typeof(IAsset), out _))
    {
      yield return new MenuItem(typeof(GetAsset<>).MakeGenericType(inputType), group: "Assets");
    }

    if (inputType.IsEnum)
    {
      // yield return new MenuItem(typeof(NextValue<>).MakeGenericType(inputType));
      // yield return new MenuItem(typeof(ShiftEnum<>).MakeGenericType(inputType));

      var enumType = inputType.GetEnumUnderlyingType();
      if (NodeUtils.TryGetNumberToEnumNode(enumType, out var toNumberType))
      {
        yield return new MenuItem(toNumberType.MakeGenericType(inputType), group: "Enums");
      }
    }

    if (inputType == typeof(int) && (
            typeof(ValueMultiplex<>).IsAssignableFrom(nodeType)
            || typeof(ObjectMultiplex<>).IsAssignableFrom(nodeType)
            || typeof(ValueDemultiplex<>).IsAssignableFrom(nodeType)
            || typeof(ObjectDemultiplex<>).IsAssignableFrom(nodeType)))
    {
      yield return new MenuItem(typeof(ImpulseDemultiplexer), name: "Impulse Demultiplexer", group: "Impulse");
      yield return new MenuItem(typeof(IndexOfFirstValueMatch<dummy>), group: "Selection");
    }


    if (TypeUtils.MatchesType(typeof(ValueMul<>), nodeType))
    {
      var atan2Type = TryGetPsuedoGenericForType(inputProxy.World, "Atan2_", nodeType.GenericTypeArguments[0]);
      var nodeHasAtan2Connection = inputProxy.Node.Target.NodeInstance.AllInputElements().Any(i => i.Source is IOutput source && source.OwnerNode.GetType() == atan2Type);
      if (nodeHasAtan2Connection)
      {
        yield return new MenuItem(typeof(RadToDeg), overload: true, group: "Conversion");
      }
    }

    // todo: playoneshot group
    if ((nodeType == typeof(PlayOneShot) || nodeType == typeof(PlayOneShotAndWait)) && inputProxy.ElementName == "Speed")
    {
      yield return new MenuItem(typeof(RandomFloat), group: "Random");
    }

    if (inputType == typeof(bool)) yield return new MenuItem(typeof(DataModelBooleanToggle), group: "Variables");

    var variableInput = GetNodeForType(inputType, [
      new NodeTypeRecord(typeof(LocalValue<>), null, null),
      new NodeTypeRecord(typeof(LocalObject<>), null, null),
    ]);
    var variableInput2 = GetNodeForType(inputType, [
      new NodeTypeRecord(typeof(StoredValue<>), null, null),
      new NodeTypeRecord(typeof(StoredObject<>), null, null),
    ]);
    var variableInput3 = GetNodeForType(inputType, [
      new NodeTypeRecord(typeof(DataModelValueFieldStore<>), null, null),
      new NodeTypeRecord(typeof(DataModelObjectRefStore<>), null, null),
    ]);

    yield return new MenuItem(variableInput, group: "Variables");
    yield return new MenuItem(variableInput2, group: "Variables");
    yield return new MenuItem(variableInput3, group: "Variables");
  }

  // Items that show up if you trigger select without any proxies
  internal static IEnumerable<MenuItem> NullProxyMenuItems()
  {
    yield return new(typeof(ObjectRelay<Slot>), binding: typeof(FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.RefObjectInput<Slot>),
      name: "Slot Input", group: "References");
    yield return new(typeof(GetActiveUserSelf), group: "References");

    yield return new(typeof(ValueWrite<int>), group: "Variables");


    yield return new(typeof(DynamicImpulseReceiver), group: "Impulse");
  }

  internal static Dictionary<Type, List<Type>> UnpackNodeMapping(World world) =>
    world.GetPsuedoGenericTypesForWorld()
          .UnpackingNodes()
          .Where(i => i.Types.Count() == 1)
          .Select(i => (i.Node, Type: i.Types.First()))
          .GroupBy(i => i.Type, i => i.Node)
          .Select(i => (i.Key, (IEnumerable<Type>)i))
          .Concat([
            (typeof(Rect), [typeof(RectToXYWH), typeof(RectToMinMax), typeof(RectToPositionSize)]),
            (typeof(SphericalHarmonicsL1<>),  [typeof(UnpackSH1<>)]),
            (typeof(SphericalHarmonicsL2<>),  [typeof(UnpackSH2<>)]),
            (typeof(SphericalHarmonicsL3<>),  [typeof(UnpackSH3<>)]),
            (typeof(SphericalHarmonicsL4<>),  [typeof(UnpackSH4<>)]),
          ])
          .ToDictionary(i => i.Item1, i => i.Item2.ToList());

  internal static bool TryGetUnpackNode(World world, Type nodeType, [NotNullWhen(true)] out List<Type>? value)
  {
    if (ReflectionHelper.IsNullable(nodeType) && Nullable.GetUnderlyingType(nodeType).IsUnmanaged() && Nullable.GetUnderlyingType(nodeType) is var underlyingType and not null)
    {
      try
      {
        value = [typeof(UnpackNullable<>).MakeGenericType(underlyingType)];
        return true;
      }
      catch
      {
        value = null;
        return false;
      }
    }
    var mappings = UnpackNodeMapping(world);
    if (TypeUtils.TryGetGenericTypeDefinition(nodeType, out var genericTypeDefinition) && mappings.TryGetValue(genericTypeDefinition, out var genericUnpackNodeTypes))
    {
      value = [.. genericUnpackNodeTypes.Select(t => t.MakeGenericType(nodeType.GenericTypeArguments))];
      return true;
    }
    else
    {
      return mappings.TryGetValue(nodeType, out value);
    }
  }

  internal static Dictionary<Type, List<Type>> PackNodeMappings(World world) =>
    world.GetPsuedoGenericTypesForWorld()
          .PackingNodes()
          .Where(i => i.Types.Count() == 1)
          .Select(i => (i.Node, Type: i.Types.First()))
          .GroupBy(i => i.Type, i => i.Node)
          .Select(i => (i.Key, (IEnumerable<Type>)i))
          .Concat([
            (typeof(Rect), [typeof(RectFromXYWH), typeof(RectFromMinMax), typeof(RectFromPositionSize)]),
            (typeof(ZitaParameters), [typeof(ConstructZitaParameters)]),
            (typeof(SphericalHarmonicsL1<>),  [typeof(PackSH1<>)]),
            (typeof(SphericalHarmonicsL2<>),  [typeof(PackSH2<>)]),
            (typeof(SphericalHarmonicsL3<>),  [typeof(PackSH3<>)]),
            (typeof(SphericalHarmonicsL4<>),  [typeof(PackSH4<>)]),
          ])
          .ToDictionary(i => i.Item1, i => i.Item2.ToList());


  internal static bool TryGetPackNode(World world, Type nodeType, [NotNullWhen(true)] out List<Type>? value)
  {
    if (ReflectionHelper.IsNullable(nodeType) && Nullable.GetUnderlyingType(nodeType).IsUnmanaged() && Nullable.GetUnderlyingType(nodeType) is Type underlyingType)
    {
      try
      {
        value = [typeof(PackNullable<>).MakeGenericType(underlyingType)];
        return true;
      }
      catch
      {
        value = null;
        return false;
      }
    }

    var mappings = PackNodeMappings(world);
    if (TypeUtils.TryGetGenericTypeDefinition(nodeType, out var genericTypeDefinition) && mappings.TryGetValue(genericTypeDefinition, out var genericUnpackNodeType))
    {
      value = [.. genericUnpackNodeType.Select(t => t.MakeGenericType(nodeType.GenericTypeArguments))];
      return true;
    }
    else
    {
      return mappings.TryGetValue(nodeType, out value);
    }
  }

  internal static readonly Dictionary<Type, Type> InverseNodeMapping = new()
    {
        {typeof(float2x2), typeof(Inverse_Float2x2)},
        {typeof(float3x3), typeof(Inverse_Float3x3)},
        {typeof(float4x4), typeof(Inverse_Float4x4)},
        {typeof(double2x2), typeof(Inverse_Double2x2)},
        {typeof(double3x3), typeof(Inverse_Double3x3)},
        {typeof(double4x4), typeof(Inverse_Double4x4)},
        // shh
        {typeof(floatQ), typeof(InverseRotation_floatQ)},
        {typeof(doubleQ), typeof(InverseRotation_doubleQ)},
    };

  internal static bool TryGetInverseNode(Type valueType, [NotNullWhen(true)] out Type? value) =>
      InverseNodeMapping.TryGetValue(valueType, out value);

  internal static readonly Dictionary<Type, Type> TransposeNodeMapping = new()
    {
        {typeof(float2x2), typeof(Transpose_Float2x2)},
        {typeof(float3x3), typeof(Transpose_Float3x3)},
        {typeof(float4x4), typeof(Transpose_Float4x4)},
        {typeof(double2x2), typeof(Transpose_Double2x2)},
        {typeof(double3x3), typeof(Transpose_Double3x3)},
        {typeof(double4x4), typeof(Transpose_Double4x4)},
    };

  internal static bool TryGetTransposeNode(Type valueType, [NotNullWhen(true)] out Type? value) =>
      TransposeNodeMapping.TryGetValue(valueType, out value);

  private static bool IsIterationNode(Type nodeType) =>
      nodeType == typeof(For)
      || nodeType == typeof(AsyncFor)
      || nodeType == typeof(While)
      || nodeType == typeof(AsyncWhile);

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxTool), "CleanupDraggedWire")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void CleanupDraggedWire(ProtoFluxTool instance) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxTool), "OnSecondaryPress")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static void OnSecondaryPress(ProtoFluxTool instance) => throw new NotImplementedException();


  [HarmonyReversePatch]
  [HarmonyPatch(typeof(ProtoFluxHelper), "GetNodeForType")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static Type GetNodeForType(Type type, List<NodeTypeRecord> list) => throw new NotImplementedException();

  [HarmonyReversePatch]
  [HarmonyPatch(typeof(Tool), "GetHit")]
  [MethodImpl(MethodImplOptions.NoInlining)]
  internal static RaycastHit? GetHit(Tool instance) => throw new NotImplementedException();
}